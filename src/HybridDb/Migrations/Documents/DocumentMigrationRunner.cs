using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Linq.Old;
using HybridDb.Queue;
using Microsoft.Extensions.Logging;
using static Indentional.Text;

namespace HybridDb.Migrations.Documents
{
    public class DocumentMigrationRunner : IDisposable
    {
        readonly DocumentStore store;
        readonly CancellationTokenSource cts;
        readonly ILogger logger;
        
        Task loop = Task.CompletedTask;

        public DocumentMigrationRunner(DocumentStore store)
        {
            this.store = store;
            logger = store.Configuration.Logger;

            cts = new CancellationTokenSource();
        }

        public Task Run()
        {
            var configuration = store.Configuration;

            if (!configuration.RunBackgroundMigrations)
                return loop;

            return loop = Task.Run(async () =>
            {
                try
                {
                    configuration.Notify(new MigrationStarted(store));

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

                            while (!cts.IsCancellationRequested)
                            {
                                try
                                {
                                    var where = command.Matches(store, migration?.Version);

                                    using var tx = store.BeginTransaction();

                                    var formattedTableName = store.Database.FormatTableNameAndEscape(table.Name);

                                    var totalResults = tx
                                        .Query<int>(new SqlBuilder(parameters: where.Parameters.Parameters.ToArray())
                                            .Append($"select count(*) from {formattedTableName} where {where}"))
                                        .First();

                                    if (totalResults == 0) break;

                                    var rows = tx
                                        .Query<object>(new SqlBuilder(parameters: where.Parameters.Parameters.ToArray())
                                            .Append($"select top {batchSize} * from {formattedTableName} with (xlock, readpast) where {where}"))
                                        .Select(x => (IDictionary<string, object>)x)
                                        .ToList();

                                    logger.LogInformation(Indent(@"
                                        Migrating {NumberOfDocumentsInBatch} documents from {Table}. 
                                        {NumberOfPendingDocuments} documents left."
                                    ), rows.Count, table.Name, totalResults);

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

                                    await Task.Delay(1000, cts.Token);
                                }
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
                        DocumentMigrationRunner failed and stopped. 
                        Documents will not be migrated in background until you restart the runner.
                        They will still be migrated on Session.Load() and Session.Query()."));
                }
                finally
                {
                    configuration.Notify(new MigrationEnded(store));
                }
            }, cts.Token);
        }

        public void Dispose()
        {
            cts.Cancel();
            
            loop.ContinueWith(x => x).Wait();
        }

        static async Task<bool> MigrateAndSave(DocumentStore store, DocumentTransaction tx, DocumentDesign baseDesign, IDictionary<string, object> row)
        {
            var key = (string) row[DocumentTable.IdColumn];
            var discriminator = ((string) row[DocumentTable.DiscriminatorColumn]).Trim();
            var concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(baseDesign, discriminator);

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