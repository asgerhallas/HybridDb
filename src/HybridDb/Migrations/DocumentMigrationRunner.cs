using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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
                    var baseDesign = configuration.TryGetDesignByTablename(table.Name);
                    if (baseDesign == null)
                    {
                        throw new InvalidOperationException($"Design not found for table '{table.Name}'");
                    }

                    var running = true;

                    while (running)
                    {
                        var rows = store
                            .Query(table, out var stats,
                                @where: "AwaitsReprojection = @AwaitsReprojection or Version < @version",
                                @select: "Id, AwaitsReprojection, Version, Discriminator, Etag",
                                take: 500,
                                parameters: new { AwaitsReprojection = true, version = configuration.ConfiguredVersion })
                            .ToList();

                        if (stats.TotalResults == 0) break;

                        logger.Information($"Migrating {stats.RetrievedResults}/{stats.TotalResults} from {table.Name}.");

                        foreach (var row in rows)
                        {
                            var key = (string)row[table.IdColumn];
                            var currentDocumentVersion = (int)row[table.VersionColumn];
                            var discriminator = ((string)row[table.DiscriminatorColumn]).Trim();
                            var concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(baseDesign, discriminator);

                            var shouldUpdate = false;

                            if ((bool)row[table.AwaitsReprojectionColumn])
                            {
                                shouldUpdate = true;
                                logger.Debug("Reprojection document {0}/{1}.",
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
                                    using (var session = new DocumentSession(store, null))
                                    {
                                        session.Load(concreteDesign.DocumentType, key);
                                        session.SaveChanges(lastWriteWins: false, forceWriteUnchangedDocument: true);
                                    }
                                }
                                catch (ConcurrencyException) { }
                                catch (SqlException) { }
                                catch (Exception exception)
                                {
                                    logger.Error(exception, 
                                        "Unrecoverable exception while migrating document of type '{type}' with id '{id}'. Stopping migrator for table '{table}'.",
                                        concreteDesign.DocumentType.FullName, key, concreteDesign.Table.Name);

                                    running = false;
                                }
                            }
                            else
                            {
                                logger.Debug("Document did not change.");

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