using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Serilog;
using static Indentional.Indent;
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

            store.Database.RawExecute($"ALTER DATABASE {(store.TableMode == TableMode.GlobalTempTables ? "TempDb" : "CURRENT")} SET ALLOW_SNAPSHOT_ISOLATION ON;");

            using (var tx = BeginTransaction())
            {
                LockDatabase();

                TryCreateMetadataTable();

                var schemaVersion = GetAndUpdateSchemaVersion(store.Configuration.ConfiguredVersion);

                if (schemaVersion > store.Configuration.ConfiguredVersion)
                {
                    throw new InvalidOperationException(_($@"
                        Database schema is ahead of configuration. Schema is version {schemaVersion}, 
                        but the highest migration version number is {store.Configuration.ConfiguredVersion}."));
                }

                var requiresReprojection = new List<string>();

                requiresReprojection.AddRange(RunAutoMigrations(schemaVersion));
                requiresReprojection.AddRange(RunConfiguredMigrations(schemaVersion));

                MarkDocumentsForReprojections(requiresReprojection);

                tx.Complete();
            }
        }


        static TransactionScope BeginTransaction() => 
            new TransactionScope(
                TransactionScopeOption.RequiresNew, 
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.Serializable,
                    Timeout = TimeSpan.FromMinutes(10)
                }, 
                TransactionScopeAsyncFlowOption.Suppress);

        void LockDatabase()
        {
            using (var connection = store.Database.Connect(true))
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
        }

        void TryCreateMetadataTable()
        {
            var metadata = new Table("HybridDb", new Column("SchemaVersion", typeof(int)));

            store.Configuration.tables.TryAdd(metadata.Name, metadata);

            store.Execute(new CreateTable(metadata));

            var hybridDbTableName = store.Database.FormatTableNameAndEscape(metadata.Name);

            store.Database.RawExecute($@"
                if not exists (select * from {hybridDbTableName})
                    insert into {hybridDbTableName} (SchemaVersion) values (-1);", 
                schema: true);
        }

        int GetAndUpdateSchemaVersion(int nextSchemaVersion)
        {
            var currentSchemaVersion = store.Database.RawQuery<int>($@"
                update {store.Database.FormatTableNameAndEscape("HybridDb")}
                set [SchemaVersion] = @nextSchemaVersion
                output DELETED.SchemaVersion", 
                new { nextSchemaVersion }, 
                schema: true
            ).SingleOrDefault();

            return currentSchemaVersion;
        }

        IEnumerable<string> RunAutoMigrations(int schemaVersion)
        {
            var schema = schemaVersion == -1 // fresh database
                ? new Dictionary<string, List<string>>()
                : store.Database.QuerySchema();

            var commands = differ.CalculateSchemaChanges(schema, store.Configuration);

            if (!commands.Any()) yield break;

            logger.Information("Found {0} differences between current schema and configuration. Migrates schema to get up to date.", commands.Count);

            foreach (var command in commands)
            {
                foreach (var tablename in ExecuteCommand(command, false))
                {
                    yield return tablename;
                }
            }
        }

        IEnumerable<string> RunConfiguredMigrations(int schemaVersion)
        {
            if (!(store.Database is SqlServerUsingRealTables))
            {
                logger.Information("Skips provided migrations when not using real tables.");
                yield break;
            }

            if (schemaVersion >= store.Configuration.ConfiguredVersion) yield break;

            var migrationsToRun = migrations.OrderBy(x => x.Version).Where(x => x.Version > schemaVersion).ToList();

            logger.Information("Migrates schema from version {0} to {1}.", schemaVersion, store.Configuration.ConfiguredVersion);

            foreach (var migration in migrationsToRun)
            {
                var migrationCommands = migration.MigrateSchema(store.Configuration);

                foreach (var command in migrationCommands)
                {
                    foreach (var tablename in ExecuteCommand(command, true))
                    {
                        yield return tablename;
                    }
                }
            }
        }

        void MarkDocumentsForReprojections(List<string> requiresReprojection)
        {
            foreach (var tablename in requiresReprojection)
            {
                var design = store.Configuration.TryGetDesignByTablename(tablename);
                if (design == null) continue;

                store.Database.RawExecute(
                    $"update {store.Database.FormatTableNameAndEscape(tablename)} set AwaitsReprojection=@AwaitsReprojection",
                    new { AwaitsReprojection = true },
                    schema: true);
            }
        }

        IEnumerable<string> ExecuteCommand(DdlCommand command, bool allowUnsafe)
        {
            if (command.Unsafe && !allowUnsafe)
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