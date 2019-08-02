using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Serilog;
using IsolationLevel = System.Transactions.IsolationLevel;

namespace HybridDb.Migrations.Schema
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

            store.Database.RawExecute($"ALTER DATABASE {(store.TableMode == TableMode.GlobalTempTables ? "TempDb" : "CURRENT")} SET ALLOW_SNAPSHOT_ISOLATION ON;");

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

                TryCreateMetadataTable(configuration);

                var schemaVersion = GetSchemaVersion(database, configuration);

                // get the diff and run commands to get to configured schema
                var schema = schemaVersion == -1
                    ? new Dictionary<string, List<string>>()
                    : database.QuerySchema();

                var commands = differ.CalculateSchemaChanges(schema, configuration);

                if (commands.Any())
                {
                    logger.Information("Found {0} differences between current schema and configuration. Migrates schema to get up to date.", commands.Count);

                    foreach (var command in commands)
                    {
                        requiresReprojection.AddRange(ExecuteCommand(command));
                    }
                }

                if (database is SqlServerUsingRealTables)
                {
                    schemaVersion = RunConfiguredMigrations(schemaVersion, configuration, requiresReprojection);
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

                UpdateSchemaVersion(database, schemaVersion);

                tx.Complete();
            }
        }

        int RunConfiguredMigrations(int schemaVersion, Configuration configuration, List<string> requiresReprojection)
        {
            if (schemaVersion < configuration.ConfiguredVersion)
            {
                var migrationsToRun = migrations.OrderBy(x => x.Version).Where(x => x.Version > schemaVersion).ToList();
                logger.Information("Migrates schema from version {0} to {1}.", schemaVersion, configuration.ConfiguredVersion);

                foreach (var migration in migrationsToRun)
                {
                    var migrationCommands = migration.MigrateSchema();
                    foreach (var command in migrationCommands)
                    {
                        requiresReprojection.AddRange(ExecuteCommand(command));
                    }

                    schemaVersion++;
                }
            }

            return schemaVersion;
        }

        void TryCreateMetadataTable(Configuration configuration)
        {
            var metadata = new Table("HybridDb", new Column("SchemaVersion", typeof(int), defaultValue: -1));
            configuration.tables.TryAdd(metadata.Name, metadata);
            store.Execute(new CreateTable(metadata));
        }

        static int GetSchemaVersion(IDatabase database, Configuration configuration)
        {
            var schemaVersion = database.RawQuery<int>($"select top 1 SchemaVersion from {database.FormatTableNameAndEscape("HybridDb")}", schema: true).SingleOrDefault();

            if (schemaVersion > configuration.ConfiguredVersion)
            {
                throw new InvalidOperationException(
                    $"Database schema is ahead of configuration. Schema is version {schemaVersion}, " +
                    $"but configuration is version {configuration.ConfiguredVersion}.");
            }

            return schemaVersion;
        }

        static void UpdateSchemaVersion(IDatabase database, int schemaVersion)
        {
            var hybridDbTableName = database.FormatTableNameAndEscape("HybridDb");

            database.RawExecute($@"
                if not exists (select * from {hybridDbTableName}) 
                    insert into {hybridDbTableName} (SchemaVersion) values (@version); 
                else
                    update {hybridDbTableName} set SchemaVersion=@version",
                new {version = Math.Max(schemaVersion, 0)}, schema: true);
        }

        IEnumerable<string> ExecuteCommand(DdlCommand command)
        {
            if (command.Unsafe)
            {
                logger.Warning("Unsafe migration command '{0}' was skipped.", command.ToString());
                yield break;
            }

            logger.Information("Executing migration command '{0}'.", command.ToString());

            store.Execute(command);

            if (command.RequiresReprojectionOf != null)
                yield return command.RequiresReprojectionOf;
        }
    }
}