using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Config;
using Microsoft.Extensions.Logging;
using static Indentional.Text;

namespace HybridDb.Migrations.Documents
{
    public class BackgroundMigrationRunner
    {
        public Task Run(DocumentStore store)
        {
            var logger = store.Configuration.Logger;
            var configuration = store.Configuration;

            if (!configuration.RunBackgroundMigrations)
                return Task.CompletedTask;

            return Task.Run(async () =>
                {
                    try
                    {
                        configuration.Notify(new MigrationStarted(store));

                        const int batchSize = 500;

                        var random = new Random();

                        foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
                        {
                            //TODO: Skip if no matching commands... but remember implicit reprojection. Can it be done?

                            // Get the commands. For each command run a "marking query". 
                            // Move above to Upfront time
                            // In background runner:
                            // For each table check if there are any marked documents - maybe check commands for optimization
                            // Load marked documents and check for sanity or throw
                            // Migrate and save the document

                            // For single loads:
                            // Check if the document is marked and migrate if so. Sanity checks apply.

                            // Motivation: 
                            // 1. Matchers that run on the background migrations are not applied to single loads
                            // So we need to use the matchers to mark docs instead of use them for background query
                            // to avoid migrating documents that were actually filtered away by an sql matcher
                            // 
                            // 2. If a row is loaded, that does not match a migration (like PatchCase when the migration is for Case)
                            // we don't want the background runner to load it at all (for performance) - or at least not try to save it (for side effects).
                            // Single loads will already not migrate based on the document type, and will not save if there are no changes.

                            var baseDesign = configuration.TryGetDesignByTablename(table.Name)
                                             ?? throw new InvalidOperationException($"Design not found for table '{table.Name}'");

                            var numberOfRowsLeft = 0;

                            while (true)
                            {
                                try
                                {
                                    var skip = numberOfRowsLeft > batchSize
                                        ? random.Next(0, numberOfRowsLeft)
                                        : 0;

                                    var rows = store
                                        .Query(table, out var stats,
                                            select: "*",
                                            where: $"[{DocumentTable.AwaitsMigrationColumn.Name}] = 1",
                                            window: new SkipTake(skip, batchSize))
                                        .ToList();

                                    if (stats.TotalResults == 0) break;

                                    numberOfRowsLeft = stats.TotalResults - stats.RetrievedResults;

                                    logger.LogInformation(Indent(@"
                                            Migrating {NumberOfDocumentsInBatch} documents from {Table}. 
                                            {NumberOfPendingDocuments} documents left."
                                    ), stats.RetrievedResults, table.Name, stats.TotalResults);

                                    using var tx = store.BeginTransaction();

                                    foreach (var row in rows)
                                    {
                                        // TODO: Can be parallel
                                        if (!await MigrateAndSave(store, tx, baseDesign, row))
                                        {
                                            goto nextTable;
                                        }
                                    }

                                    tx.Complete();
                                }
                                catch (SqlException exception)
                                {
                                    store.Logger.LogWarning(exception,
                                        "SqlException while migrating documents from table '{table}'. Will retry in 1s.",
                                        table.Name);

                                    await Task.Delay(1000);
                                }
                            }

                            logger.LogInformation("Documents in {Table} are fully migrated to {Version}.",
                                table.Name,
                                store.Configuration.ConfiguredVersion);

                            nextTable: ;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, Indent(@"
                            BackgroundMigrationRunner failed and stopped. 
                            Documents will not be migrated in background until you restart the runner.
                            They will still be migrated on Session.Load() and Session.Query()."));
                    }
                    finally
                    {
                        configuration.Notify(new MigrationEnded(store));
                    }
                });
        }

        static async Task<bool> MigrateAndSave(DocumentStore store, DocumentTransaction tx, DocumentDesign baseDesign, IDictionary<string, object> row)
        {
            var key = row.Get(DocumentTable.IdColumn);
            var discriminator = row.Get(DocumentTable.DiscriminatorColumn).Trim();
            var concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(baseDesign, discriminator);

            if (!row.Get(DocumentTable.AwaitsMigrationColumn))
            {
                throw new ArgumentException($"Row '{key}' is not marked for migration.");
            }

            try
            {
                using var session = new DocumentSession(store, store.Migrator, tx);

                session.ConvertToEntityAndPutUnderManagement(concreteDesign.DocumentType, concreteDesign, row, readOnly: false);
                session.SaveChanges(false, true);
            }
            catch (ConcurrencyException exception)
            {
                store.Logger.LogInformation(exception,
                    "ConcurrencyException while migrating document of type '{type}' with id '{id}'. Document is migrated by the other party.",
                    concreteDesign.DocumentType.FullName, key);
            }
            catch (SqlException exception)
            {
                store.Logger.LogWarning(exception,
                    "SqlException while migrating document of type '{type}' with id '{id}'. Will retry in 1s.",
                    concreteDesign.DocumentType.FullName, key);

                await Task.Delay(1000);
            }
            catch (Exception exception)
            {
                store.Logger.LogError(exception,
                    "Unrecoverable exception while migrating document of type '{type}' with id '{id}'. Stopping migrator for table '{table}'.",
                    concreteDesign.DocumentType.FullName, key,
                    concreteDesign.Table.Name);

                return false;
            }

            return true;
        }
    }
}