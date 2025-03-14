using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Migrations.Schema.Commands;
using Microsoft.Extensions.Logging;
using static Indentional.Text;
using IsolationLevel = System.Transactions.IsolationLevel;

namespace HybridDb.Migrations.Schema
{
    public class SchemaMigrationRunner
    {
        static object locker = new();

        readonly ILogger logger;
        readonly DocumentStore store;
        readonly IReadOnlyList<Migration> migrations;
        readonly ISchemaDiffer differ;

        public SchemaMigrationRunner(DocumentStore store, ISchemaDiffer differ)
        {
            this.store = store;
            this.differ = differ;

            logger = store.Configuration.Logger;
            migrations = store.Configuration.Migrations.ToList();
        }

        public void Run()
        {
            store.Configuration.Initialize();
            store.Database.Initialize();
            
            Migrate(store.TableMode == TableMode.GlobalTempTables, () =>
            {
                TryCreateMetadataTable();

                var schemaVersion = GetAndUpdateSchemaVersion(store.Configuration.ConfiguredVersion);

                if (schemaVersion > store.Configuration.ConfiguredVersion)
                {
                    throw new InvalidOperationException(Indent($@"
                        Database schema is ahead of configuration. Schema is version {schemaVersion},
                        but the highest migration version number is {store.Configuration.ConfiguredVersion}."));
                }

                MarkDocumentsForReprojections(RunMigrations(schemaVersion));
            });
        }


        void Migrate(bool isTempTables, Action action)
        {
            var sw = Stopwatch.StartNew();

            store.Database.RawExecute($"ALTER DATABASE {(store.TableMode == TableMode.GlobalTempTables ? "TempDb" : "CURRENT")} SET ALLOW_SNAPSHOT_ISOLATION ON;");

            if (isTempTables)
            {
                action();
            }
            else
            {
                lock (locker)
                {
                    using (var tx = BeginTransaction())
                    {
                        LockDatabase();

                        action();

                        tx.Complete();
                    }
                }
            }

            logger.LogInformation($"Schema migrations ran in {{time}}ms on {{tables}}.", sw.ElapsedMilliseconds, isTempTables ? "temp tables" : "real tables");
        }

        static TransactionScope BeginTransaction() => 
            new(
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
                parameters.Add("@Resource", $"HybridDb");
                parameters.Add("@DbPrincipal", "public");
                parameters.Add("@LockMode", "Exclusive");
                parameters.Add("@LockOwner", "Transaction");

                // Time allowed to obtain the lock, before sp_getapplock return with -1 return code
                parameters.Add("@LockTimeout", TimeSpan.FromSeconds(300).TotalMilliseconds);
                parameters.Add("@Result", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

                connection.Connection.Execute(@"sp_getapplock", parameters, commandType: CommandType.StoredProcedure, commandTimeout: 300);

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
            var metadata = store.Configuration.GetMetadataTable();

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

        IReadOnlyList<string> RunMigrations(int schemaVersion)
        {
            var configuredMigrations = GetConfiguredMigrations(schemaVersion);

            if (!configuredMigrations.Any())
            {
                return GetReprojections(RunAutoMigrations(schemaVersion, hasMigrationsBeforeAutoMigrations: false));
            }

            logger.LogInformation("Found migrations from version {0} to {1}.", schemaVersion, store.Configuration.ConfiguredVersion);

            var requiresReprojection = new List<string>();

            var beforeCommands = RunConfiguredMigrations(configuredMigrations, x => x.BeforeAutoMigrations(store.Configuration)).ToList();
            requiresReprojection.AddRange(GetReprojections(beforeCommands));

            requiresReprojection.AddRange(GetReprojections(RunAutoMigrations(schemaVersion, hasMigrationsBeforeAutoMigrations: beforeCommands.Any())));
            
            var afterCommands = RunConfiguredMigrations(configuredMigrations, x => x.AfterAutoMigrations(store.Configuration));
            requiresReprojection.AddRange(GetReprojections(afterCommands));

            logger.LogInformation("Schema is migrated from version {0} to {1}.", schemaVersion, store.Configuration.ConfiguredVersion);

            return requiresReprojection;
        }

        IReadOnlyList<string> GetReprojections(IEnumerable<DdlCommand> commands) => commands
            .Where(x => !string.IsNullOrEmpty(x.RequiresReprojectionOf))
            .Select(x => x.RequiresReprojectionOf)
            .ToList();

        IReadOnlyList<DdlCommand> RunAutoMigrations(int schemaVersion, bool hasMigrationsBeforeAutoMigrations)
        {
            var schema = schemaVersion == -1 && !hasMigrationsBeforeAutoMigrations // fresh database
                ? new Dictionary<string, List<string>>()
                : store.Database.QuerySchema();

            var commands = differ.CalculateSchemaChanges(schema, store.Configuration);

            if (!commands.Any()) return new List<DdlCommand>();

            logger.LogInformation("Found {0} differences between current schema and configuration. Automatically migrates schema to get up to date.", commands.Count);

            return commands.SelectMany(command => ExecuteCommand(command, false)).ToList();
        }

        IEnumerable<DdlCommand> RunConfiguredMigrations(
            IEnumerable<Migration> migrationsToRun, 
            Func<Migration, IEnumerable<DdlCommand>> selector)
        {
            foreach (var migration in migrationsToRun)
            {
                foreach (var command in selector(migration))
                {
                    foreach (var executedCommand in ExecuteCommand(command, true))
                    {
                        yield return executedCommand;
                    }
                }
            }
        }

        IReadOnlyList<Migration> GetConfiguredMigrations(int schemaVersion)
        {
            if (store.Database is not SqlServerUsingRealTables && !store.Configuration.RunUpfrontMigrationsOnTempTables)
            {
                logger.LogInformation("Skips provided migrations when not using real tables.");
                return new List<Migration>();
            }

            if (schemaVersion >= store.Configuration.ConfiguredVersion) return new List<Migration>();

            return migrations
                .OrderBy(x => x.Version)
                .Where(x => x.Version > schemaVersion)
                .ToList();
        }

        void MarkDocumentsForReprojections(IEnumerable<string> requiresReprojection)
        {
            foreach (var tablename in requiresReprojection.Distinct())
            {
                var design = store.Configuration.TryGetDesignByTablename(tablename);
                if (design == null) continue;

                store.Database.RawExecute(
                    $"update {store.Database.FormatTableNameAndEscape(tablename)} set AwaitsReprojection=@AwaitsReprojection",
                    new { AwaitsReprojection = true },
                    schema: true,
                    commandTimeout: 300);
            }
        }

        IEnumerable<DdlCommand> ExecuteCommand(DdlCommand command, bool allowUnsafe)
        {
            if (!command.Safe && !allowUnsafe)
            {
                logger.LogWarning("Unsafe migration command '{0}' was skipped.", command.ToString());
                yield break;
            }

            logger.LogInformation("Executing migration command '{0}'.", command.ToString());

            store.Execute(command);

            yield return command;
        }
    }
}