using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Config;
using Serilog;

namespace HybridDb.Migrations
{
    public class DocumentMigrationRunner
    {
        public Task Run(IDocumentStore store)
        {
            var logger = store.Configuration.Logger;
            var configuration = store.Configuration;

            if (!configuration.RunDocumentMigrationsOnStartup)
                return Task.FromResult(0);

            return Task.Factory.StartNew(() =>
            {
                var migrator = new DocumentMigrator(configuration);

                foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
                {
                    var baseDesign = configuration.DocumentDesigns.First(x => x.Table.Name == table.Name);

                    while (true)
                    {
                        QueryStats stats;

                        var rows = store
                            .Query(table, out stats,
                                @where: "AwaitsReprojection = @AwaitsReprojection or Version < @version",
                                @select: "Id, AwaitsReprojection, Version, Discriminator, Etag",
                                take: 100,
                                @orderby: "newid()",
                                parameters: new { AwaitsReprojection = true, version = configuration.ConfiguredVersion })
                            .ToList();

                        if (stats.TotalResults == 0) break;

                        logger.Information("Found {0} document that must be migrated.", stats.TotalResults);

                        foreach (var row in rows)
                        {
                            var key = (string)row[table.IdColumn];
                            var currentDocumentVersion = (int)row[table.VersionColumn];
                            var discriminator = ((string)row[table.DiscriminatorColumn]).Trim();
                            var concreteDesign = store.Configuration.GetOrCreateConcreteDesign(baseDesign, discriminator, key);

                            var shouldUpdate = false;

                            if ((bool)row[table.AwaitsReprojectionColumn])
                            {
                                shouldUpdate = true;
                                logger.Information("Reprojection document {0}/{1}.",
                                    concreteDesign.DocumentType.FullName, key, currentDocumentVersion, configuration.ConfiguredVersion);
                            }

                            if (migrator.ApplicableCommands(concreteDesign, currentDocumentVersion).Any())
                            {
                                shouldUpdate = true;
                            }

                            if (shouldUpdate)
                            {
                                try
                                {
                                    using (var session = new DocumentSession(store))
                                    {
                                        session.Load(concreteDesign, key);
                                        session.SaveChanges(lastWriteWins: false, forceWriteUnchangedDocument: true);
                                    }
                                }
                                catch (ConcurrencyException) { }
                                catch (Exception exception)
                                {
                                    logger.Error(exception, "Error while migrating document of type {0} with id {1}.", concreteDesign.DocumentType.FullName, key);
                                    Thread.Sleep(100);
                                }
                            }
                            else
                            {
                                logger.Information("Document did not change.");

                                var projection = new Dictionary<string, object>
                            {
                                {table.VersionColumn, configuration.ConfiguredVersion}
                            };

                                store.Update(table, key, (Guid)row[table.EtagColumn], projection);
                            }
                        }
                    }
                }

            }, TaskCreationOptions.LongRunning);
        }
    }
}