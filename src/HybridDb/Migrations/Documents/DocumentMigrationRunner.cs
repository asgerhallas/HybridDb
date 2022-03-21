using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Linq.Old;
using Microsoft.Extensions.Logging;
using static Indentional.Text;

namespace HybridDb.Migrations.Documents
{
    public class DocumentMigrationRunner
    {
        public Task Run(DocumentStore store)
        {
            var logger = store.Configuration.Logger;
            var configuration = store.Configuration;

            if (!configuration.RunBackgroundMigrations)
                return Task.CompletedTask;

            return Task.Factory.StartNew(async () =>
                {
                    const int batchSize = 500;

                    var random = new Random();

                    foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
                    {
                        var commands = configuration.Migrations
                            .SelectMany(x => x.Background(configuration),
                                (migration, command) => (Migration: migration, Command: command))
                            .Concat((null, new UpdateProjectionsMigration()))
                            .Where(x => x.Command.Matches(configuration, table));

                        foreach (var migrationAndCommand in commands)
                        {
                            var (migration, command) = migrationAndCommand;

                            var baseDesign = configuration.TryGetDesignByTablename(table.Name)
                                             ?? throw new InvalidOperationException($"Design not found for table '{table.Name}'");

                            var numberOfRowsLeft = 0;

                            while (true)
                            {
                                try
                                {
                                    var where = command.Matches(migration?.Version);

                                    var skip = numberOfRowsLeft > batchSize
                                        ? random.Next(0, numberOfRowsLeft)
                                        : 0;

                                    var rows = store
                                        .Query(table, out var stats,
                                            select: "*",
                                            where: where.ToString(),
                                            window: new SkipTake(skip, batchSize),
                                            parameters: where.Parameters)
                                        .ToList();

                                    if (stats.TotalResults == 0) break;

                                    numberOfRowsLeft = stats.TotalResults - stats.RetrievedResults;

                                    logger.LogInformation(Indent(
                                        @"Migrating {NumberOfDocumentsInBatch} documents from {Table}. 
                                        {NumberOfPendingDocuments} documents left."),
                                        stats.RetrievedResults, table.Name, stats.TotalResults);

                                    using var tx = store.BeginTransaction();
                                    
                                    foreach (var row in rows)
                                    {
                                        if (!await MigrateAndSave(store, tx, baseDesign, row))
                                            goto nextTable;
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
                        }

                        logger.LogInformation("Documents in {Table} are fully migrated to {Version}.",
                            table.Name,
                            store.Configuration.ConfiguredVersion);

                        nextTable:;
                    }
                },
                TaskCreationOptions.LongRunning)
            .ContinueWith(t =>
                logger.LogError(t.Exception, Indent(@"
                    DocumentMigrationRunner failed and stopped. 
                    Documents will not be migrated in background until you restart the runner.
                    They will still be migrated on Session.Load() and Session.Query()."),
                    TaskContinuationOptions.OnlyOnFaulted));
        }

        static async Task<bool> MigrateAndSave(DocumentStore store, DocumentTransaction tx, DocumentDesign baseDesign, IDictionary<string, object> row)
        {
            var key = (string) row[DocumentTable.IdColumn];
            var discriminator = ((string) row[DocumentTable.DiscriminatorColumn]).Trim();
            var concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(baseDesign, discriminator);

            try
            {
                using var session = new DocumentSession(store, store.Migrator, tx);

                session.ConvertToEntityAndPutUnderManagement(concreteDesign, row);
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