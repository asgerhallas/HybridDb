using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using HybridDb.Config;
using HybridDb.Linq;
using HybridDb.Logging;

namespace HybridDb.Migration
{
    public class MigrationRunner
    {
        readonly ILogger logger;
        readonly IMigrationProvider provider;
        readonly ISchemaDiffer differ;

        public MigrationRunner(ILogger logger, IMigrationProvider provider, ISchemaDiffer differ)
        {
            this.logger = logger;
            this.provider = provider;
            this.differ = differ;
        }

        public Task Migrate(DocumentStore store, Configuration configuration)
        {
            var metadata = new Table("HybridDb", new Column("SchemaVersion", typeof(int)));
            configuration.Tables.TryAdd(metadata.Name, metadata);

            var requiresReprojection = new List<string>();

            using (var tx = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions {IsolationLevel = IsolationLevel.Serializable}))
            {
                // abstract out so version is store independent
                var database = store.Database;
                var schema = database.QuerySchema().Values.ToList(); // demeter go home!

                var currentVersion = schema.Any(x => x.Name == "HybridDb")
                    ? database.RawQuery<int>(string.Format("select top 1 SchemaVersion from {0}",
                        database.FormatTableName("HybridDb"))).SingleOrDefault()
                    : 0;

                var enumerable = provider.GetMigrations().ToList();
                var migrations = enumerable.Where(x => x.Version > currentVersion).ToList();

                foreach (var migration in migrations)
                {
                    var migrationCommands = migration.Migrate();
                    foreach (var command in migrationCommands.OfType<SchemaMigrationCommand>())
                    {
                        if (command.Unsafe)
                        {
                            logger.Warn("Unsafe migration command '{0}' was skipped.", command);
                            continue;
                        }

                        command.Execute(database);
                    }

                    currentVersion++;
                }

                var commands = differ.CalculateSchemaChanges(schema, configuration);
                foreach (var command in commands)
                {
                    if (command.Unsafe)
                    {
                        logger.Warn("Unsafe migration command '{0}' was skipped.", command);
                        continue;
                    }

                    command.Execute(database);

                    if (command.RequiresReprojectionOf != null)
                    {
                        requiresReprojection.Add(command.RequiresReprojectionOf);
                    }
                }

                foreach (var tablename in requiresReprojection)
                {
                    // TODO: Only set RequireReprojection on command if it is documenttable - can it be done?
                    var design = configuration.DocumentDesigns.FirstOrDefault(x => x.Table.Name == tablename);
                    if (design == null) continue;
                    
                    database.RawExecute(string.Format("update {0} set state=@state", database.FormatTableNameAndEscape(tablename)), new { state = "RequiresReprojection" });
                }

                database.RawExecute(string.Format(@"
if not exists (select * from {0}) 
    insert into {0} (SchemaVersion) values (@version); 
else
    update {0} set SchemaVersion=@version",
                database.FormatTableName("HybridDb")),
                new { version = currentVersion });

                tx.Complete();
            }

            return new Task(() =>
            {
                foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
                {
                    var design = configuration.DocumentDesigns.First(x => x.Table.Name == table.Name);

                    QueryStats stats;
                    foreach (var doc in store.Query(table, out stats, where: "State = @state", parameters: new { state = "RequiresReprojection" }))
                    {
                        var discriminator = ((string)doc[table.DiscriminatorColumn]).Trim();

                        DocumentDesign concreteDesign;
                        if (!design.DecendentsAndSelf.TryGetValue(discriminator, out concreteDesign))
                        {
                            throw new InvalidOperationException(string.Format("Discriminator '{0}' was not found in configuration.", discriminator));
                        }

                        var entity = configuration.Serializer.Deserialize((byte[])doc[table.DocumentColumn], concreteDesign.DocumentType);

                        var projections = design.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(entity));
                        projections.Add("State", null);

                        store.Update(table, (Guid) doc[table.IdColumn], (Guid) doc[table.EtagColumn], projections);
                    }
                }

            }, TaskCreationOptions.LongRunning);
        }

        static void OpenAll<T>(IDocumentStore store) where T : class
        {
            int i = 0;
            while (true)
            {
                var tableName = store.Configuration.GetDesignFor<T>();
                using (var session = store.OpenSession())
                {
                    QueryStats stats;
                    var items = session.Query<T>().Statistics(out stats).Where(x => x.Column<int>("Version") == 0).Take(100).ToList();

                    i += stats.RetrievedResults;
                    Console.WriteLine("Opening {0} {1} / {2}", tableName, i, stats.TotalResults);

                    if (items.Count == 0)
                        break;

                    session.SaveChanges();
                    Console.WriteLine("Saved the changes");
                }
            }
        }

    }
}