using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Config;
using Serilog;

namespace HybridDb.Migrations
{
    public class DocumentMigrationRunner
    {
        readonly ILogger logger;
        readonly IDocumentStore store;
        readonly Configuration configuration;

        public DocumentMigrationRunner(IDocumentStore store)
        {
            this.store = store;

            logger = store.Configuration.Logger;
            configuration = store.Configuration;
        }

        public Task RunInBackground()
        {
            if (store.Configuration.RunDocumentMigrationsInBackground)
            {
                return Task.Factory.StartNew(RunSynchronously, TaskCreationOptions.LongRunning);
            }

            return Task.FromResult(false);
        }

        public void RunSynchronously()
        {
            var migrator = new DocumentMigrator(store.Configuration);

            foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
            {
                var baseDesign = configuration.DocumentDesigns.First(x => x.Table.Name == table.Name);

                while (true)
                {
                    QueryStats stats;
                    var rows = store
                        .Query(table, out stats,
                            @where: "AwaitsReprojection = @AwaitsReprojection or Version < @version",
                            take: 100,
                            orderby: "newid()",
                            parameters: new { AwaitsReprojection = true, version = configuration.ConfiguredVersion });

                    if (stats.TotalResults == 0) break;

                    logger.Information("Found {0} document that must be migrated.", stats.TotalResults);

                    foreach (var row in rows)
                    {
                        var discriminator = ((string)row[table.DiscriminatorColumn]).Trim();

                        DocumentDesign concreteDesign;
                        if (!baseDesign.DecendentsAndSelf.TryGetValue(discriminator, out concreteDesign))
                        {
                            throw new InvalidOperationException(
                                string.Format("Discriminator '{0}' was not found in configuration.", discriminator));
                        }

                        var id = (Guid)row[table.IdColumn];
                        var currentDocumentVersion = (int)row[table.VersionColumn];

                        if ((bool)row["AwaitsReprojection"])
                        {
                            logger.Information("Reprojection document {0}/{1}.", 
                                concreteDesign.DocumentType.FullName, id, currentDocumentVersion, configuration.ConfiguredVersion);
                        }

                        try
                        {
                            var entity = migrator.DeserializeAndMigrate(concreteDesign, id, (byte[])row[table.DocumentColumn], currentDocumentVersion);
                            var projections = concreteDesign.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(entity));

                            store.Update(table, id, (Guid)row[table.EtagColumn], projections);
                        }
                        catch (ConcurrencyException) { }
                        catch (Exception exception)
                        {
                            logger.Error(exception, "Error while migrating document of type {0} with id {1}.", concreteDesign.DocumentType.FullName, id);
                            Thread.Sleep(100);
                        }
                    }
                }
            }
        }
    }
}