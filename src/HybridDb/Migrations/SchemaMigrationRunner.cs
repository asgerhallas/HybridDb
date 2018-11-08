using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Serilog;
using IsolationLevel = System.Transactions.IsolationLevel;

namespace HybridDb.Migrations
{
    public class SchemaMigrationRunner
    {
        readonly ILogger logger;
        readonly DocumentStore store;
        readonly IReadOnlyList<Migration> migrations;
        readonly ISchemaDiffer differ;

        public SchemaMigrationRunner(DocumentStore store, ISchemaDiffer differ)
        {
            this.store = store;
            this.differ = differ;

            logger = store.Configuration.Logger;
            migrations = store.Configuration.Migrations;
        }

        public void Run()
        {
            if (!store.Configuration.RunSchemaMigrationsOnStartup)
                return;

            var requiresReprojection = new List<string>();

            var database = store.Database;
            var configuration = store.Configuration;

            using (var tx = new TransactionScope(
                TransactionScopeOption.RequiresNew, 
                new TransactionOptions { IsolationLevel = IsolationLevel.Serializable, Timeout = TimeSpan.FromMinutes(10) }, 
                TransactionScopeAsyncFlowOption.Suppress))
            {
                using (var connection = database.Connect(true))
                {
                    var parameters = new DynamicParameters();
                    parameters.Add("@Resource", "HybridDb");
                    parameters.Add("@DbPrincipal", "public");
                    parameters.Add("@LockMode", "Exclusive");
                    parameters.Add("@LockOwner", "Transaction");
                    parameters.Add("@LockTimeout", TimeSpan.FromSeconds(10).TotalMilliseconds);
                    parameters.Add("@Result", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

                    connection.Connection.Execute(@"sp_getapplock", parameters, commandType: CommandType.StoredProcedure);

                    var result = parameters.Get<int>("@Result");

                    if (result < 0)
                    {
                        throw new InvalidOperationException($"sp_getapplock failed with code {result}.");
                    }

                    connection.Complete();
                }

                // create metadata table if it does not exist
                var metadata = new Table("HybridDb", new Column("SchemaVersion", typeof(int)));
                configuration.tables.TryAdd(metadata.Name, metadata);
                new CreateTable(metadata, false).Execute(database);

                // get schema version
                var hybridDbTableName = database.FormatTableNameAndEscape("HybridDb");

                var currentSchemaVersion = database.RawQuery<int>($"select top 1 SchemaVersion from {hybridDbTableName}", schema: true).SingleOrDefault();

                if (currentSchemaVersion > store.Configuration.ConfiguredVersion)
                {
                    throw new InvalidOperationException(
                        $"Database schema is ahead of configuration. Schema is version {currentSchemaVersion}, " +
                        $"but configuration is version {store.Configuration.ConfiguredVersion}.");
                }

                // get the diff and run commands to get to configured schema
                var schema = database is SqlServerUsingLocalTempTables
                    ? new Dictionary<string, List<string>>()
                    : database.QuerySchema();

                var commands = differ.CalculateSchemaChanges(schema, configuration);

                if (commands.Any())
                {
                    logger.Information("Found {0} differences between current schema and configuration. Migrates schema to get up to date.", commands.Count);

                    foreach (var command in commands)
                    {
                        requiresReprojection.AddRange(ExecuteCommand(database, command));
                    }
                }

                // run provided migrations only if we are using real tables
                if (database is SqlServerUsingRealTables)
                {
                    if (currentSchemaVersion < configuration.ConfiguredVersion)
                    {
                        var migrationsToRun = migrations.OrderBy(x => x.Version).Where(x => x.Version > currentSchemaVersion).ToList();
                        logger.Information("Migrates schema from version {0} to {1}.", currentSchemaVersion, configuration.ConfiguredVersion);

                        foreach (var migration in migrationsToRun)
                        {
                            var migrationCommands = migration.MigrateSchema();
                            foreach (var command in migrationCommands)
                            {
                                requiresReprojection.AddRange(ExecuteCommand(database, command));
                            }

                            currentSchemaVersion++;
                        }
                    }
                }
                else
                {
                    logger.Information("Skips provided migrations when not using real tables.");
                }

                // flag each document of tables that need to run a re-projection
                foreach (var tablename in requiresReprojection)
                {
                    // TODO: Only set RequireReprojection on command if it is documenttable - can it be done?
                    var design = configuration.TryGetDesignByTablename(tablename);
                    if (design == null) continue;

                    database.RawExecute(
                        $"update {database.FormatTableNameAndEscape(tablename)} set AwaitsReprojection=@AwaitsReprojection",
                        new {AwaitsReprojection = true});
                }

                // update the schema version
                database.RawExecute($@"
                    if not exists (select * from {hybridDbTableName}) 
                        insert into {hybridDbTableName} (SchemaVersion) values (@version); 
                    else
                        update {hybridDbTableName} set SchemaVersion=@version",
                    new {version = currentSchemaVersion}, schema: true);

                tx.Complete();

                // For test ShouldTryNotToDeadlockOnSchemaMigationsForTempTables: Console.WriteLine($"Released lock: {Thread.CurrentThread.ManagedThreadId}.");
            }
        }

        IEnumerable<string> ExecuteCommand(IDatabase database, SchemaMigrationCommand command)
        {
            if (command.Unsafe)
            {
                logger.Warning("Unsafe migration command '{0}' was skipped.", command.ToString());
                yield break;
            }

            logger.Information("Executing migration command '{0}'.", command.ToString());

            command.Execute(database);

            if (command.RequiresReprojectionOf != null)
                yield return command.RequiresReprojectionOf;
        }
    }
}