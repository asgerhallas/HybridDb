using System;
using System.Data.SqlClient;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Linq.Old;
using Microsoft.Extensions.Logging;

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

            return Task.Factory.StartNew(() =>
            {
                foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
                {
                    var commands = configuration.Migrations
                        .SelectMany(x => x.Background(configuration), (migration, command) => (Migration: migration, Command: command))
                        .Concat((null, new UpdateProjectionsMigration()))
                        .Where(x => x.Command.Matches(configuration, table));

                    foreach (var migrationAndCommand in commands)
                    {
                        var (migration, command) = migrationAndCommand;

                        var baseDesign = configuration.TryGetDesignByTablename(table.Name) ?? throw new InvalidOperationException($"Design not found for table '{table.Name}'");

                        while (true)
                        {
                            var @where = command.Matches(migration?.Version);

                            var rows = store
                                .Query(table, out var stats, @select: "*", @where: @where.ToString(), window: new SkipTake(0, 500), parameters: @where.Parameters)
                                .ToList();

                            if (stats.TotalResults == 0) break;

                            logger.LogInformation(
                                "Migrating {NumberOfDocumentsInBatch} documents from {Table}. {NumberOfPendingDocuments} documents left.", 
                                stats.RetrievedResults, table.Name, stats.TotalResults);

                            using (var tx = store.BeginTransaction())
                            {
                                foreach (var row in rows)
                                {
                                    var key = (string) row[DocumentTable.IdColumn];
                                    var discriminator = ((string) row[DocumentTable.DiscriminatorColumn]).Trim();
                                    var concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(baseDesign, discriminator);

                                    try
                                    {
                                        using (var session = new DocumentSession(store, store.Migrator, tx))
                                        {
                                            session.ConvertToEntityAndPutUnderManagement(concreteDesign, row);
                                            session.SaveChanges(lastWriteWins: false, forceWriteUnchangedDocument: true);
                                        }
                                    }
                                    catch (ConcurrencyException exception)
                                    {
                                        logger.LogInformation(exception,
                                            "ConcurrencyException while migrating document of type '{type}' with id '{id}'. Document is migrated by the other party.",
                                            concreteDesign.DocumentType.FullName, key);
                                    }
                                    catch (SqlException exception)
                                    {
                                        logger.LogWarning(exception,
                                            "SqlException while migrating document of type '{type}' with id '{id}'. Will retry.",
                                            concreteDesign.DocumentType.FullName, key);
                                    }
                                    catch (Exception exception)
                                    {
                                        logger.LogError(exception,
                                            "Unrecoverable exception while migrating document of type '{type}' with id '{id}'. Stopping migrator for table '{table}'.",
                                            concreteDesign.DocumentType.FullName, key, concreteDesign.Table.Name);

                                        goto nextTable;
                                    }
                                }

                                tx.Complete();
                            }
                        }
                    }

                    logger.LogInformation("Documents in {Table} are fully migrated to {Version}", table.Name, store.Configuration.ConfiguredVersion);

                    nextTable:;
                }

            }, TaskCreationOptions.LongRunning);
        }
    }
}