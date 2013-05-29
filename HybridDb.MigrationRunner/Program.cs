using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Registration;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using HybridDb.Migration;
using HybridDb.Schema;

namespace HybridDb.MigrationRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("--run"))
            {
                var connectionString = ConfigurationManager.ConnectionStrings["HybridDBConnectionString"].ConnectionString;
                var store = new DocumentStore(connectionString);

                var runner = new Runner(store);

                var registration = new RegistrationBuilder();
                registration.ForTypesDerivedFrom<Migration.Migration>().Export<Migration.Migration>();

                var catalog = new DirectoryCatalog(".", registration);
                var container = new CompositionContainer(catalog);
                container.ComposeParts(runner);

                while (true)
                {
                    catalog.Refresh();
                    runner.Run();
                    Thread.Sleep(1000);
                    if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Q)
                        return;
                }
            }

            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            var executable = assembly.Location;
            var setup = new AppDomainSetup
            {
                ApplicationBase = Path.GetDirectoryName(assembly.Location),
                ShadowCopyFiles = "true"
            };

            var domain = AppDomain.CreateDomain("MigrationRunnerShadow", null, setup);
            domain.ExecuteAssembly(executable, new[] { "--run" });
        }

        public class Runner
        {
            readonly IDocumentStore store;

            [ImportMany(typeof(Migration.Migration), AllowRecomposition = true)]
            public IEnumerable<Migration.Migration> Migrations { get; set; }

            public Runner(IDocumentStore store)
            {
                this.store = store;
            }

            public void Run()
            {
                Console.SetCursorPosition(0, 0);

                var migrator = new DocumentMigrator();

                var migration = Migrations.FirstOrDefault();
                if (migration == null)
                    return;

                Console.Write(migration.GetType().Name);

                var documentMigration = migration.DocumentMigration;
                if (documentMigration != null)
                {
                    if (documentMigration.Tablename == null)
                        throw new ArgumentException("Document migration must have a tablename");

                    if (documentMigration.Version == 0)
                        throw new ArgumentException("Document migration must have a version number larger than 0");

                    var table = new Table(documentMigration.Tablename);
                    while (true)
                    {
                        QueryStats stats;
                        var @where = string.Format("Version < {0}", documentMigration.Version);

                        var rows = store.Query<object>(table, out stats, @where: @where, take: 100).Cast<IDictionary<string, object>>();
                        Console.SetCursorPosition(Console.WindowWidth-10, 0);
                        Console.Write(stats.TotalResults.ToString().PadLeft(10));

                        if (stats.TotalResults == 0)
                            break;

                        foreach (var row in rows)
                        {
                            migrator.OnRead(migration, row);

                            var id = (Guid) row[table.IdColumn.Name];
                            var etag = (Guid) row[table.EtagColumn.Name];
                            var document = (byte[]) row[table.DocumentColumn.Name];

                            try
                            {
                                //store.Update(table, id, etag, document, row);
                            }
                            catch (ConcurrencyException)
                            {
                                // We don't care. Either the version is bumped by other user or we'll retry in next round.
                            }
                        }
                    }
                }

                Console.WriteLine();

                if (Migrations.Count() > 1)
                {
                    Console.WriteLine("Migration queue:");
                    foreach (var queuedMigration in Migrations.Skip(1))
                    {
                        Console.Write(queuedMigration.GetType().Name);
                    }
                }
            }
        }
    }
}
