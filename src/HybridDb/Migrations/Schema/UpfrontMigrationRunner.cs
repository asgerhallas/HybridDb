using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Microsoft.Extensions.Logging;
using static Indentional.Text;
using IsolationLevel = System.Transactions.IsolationLevel;

namespace HybridDb.Migrations.Schema
{
    public class UpfrontMigrationRunner
    {
        static readonly object locker = new();

        readonly ILogger logger;
        readonly DocumentStore store;
        readonly IReadOnlyList<Migration> migrations;
        readonly ISchemaDiffer differ;

        public UpfrontMigrationRunner(DocumentStore store, ISchemaDiffer differ)
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

            logger.LogInformation("HybridDb startup: Initializing store and database.");

            Migrate(store.TableMode == TableMode.GlobalTempTables, () =>
            {
                TryCreateMetadataTable();

                var oldSchemaVersion = GetAndUpdateSchemaVersion(store.Configuration.ConfiguredVersion);

                logger.LogInformation($"HybridDb startup: Database is version {oldSchemaVersion}. Application is version {store.Configuration.ConfiguredVersion}.");

                if (oldSchemaVersion > store.Configuration.ConfiguredVersion)
                {
                    throw new InvalidOperationException(Indent($@"
                        Database schema is ahead of configuration. Schema is version {oldSchemaVersion},
                        but the highest migration version number is {store.Configuration.ConfiguredVersion}."));
                }

                logger.LogInformation($"HybridDb startup: Looking for upfront migrations.");

                var awaitsMigration = RunMigrations(oldSchemaVersion)
                    .Select(x => (x, new SqlBuilder().Append("1=1")))
                    .ToList();

                logger.LogInformation($"HybridDb startup: Looking for document types that are not explicitly configured.");

                CheckAndUpdateConfiguration();

                logger.LogInformation($"HybridDb startup: Looking for background migrations.");

                awaitsMigration.AddRange(FindPredicatesForBackgroundMigrations(oldSchemaVersion));

                MarkDocumentsForBackgroundMigration(awaitsMigration);
            });
        }


        void CheckAndUpdateConfiguration()
        {
            var builder = SqlBuilder.Join("union", store.Configuration
                .Tables.Values.OfType<DocumentTable>()
                .Select(table => new SqlBuilder()
                    .Append($"select '{table}' as [table], discriminator")
                    .Append($"from {store.Database.FormatTableNameAndEscape(table.Name)} group by discriminator"))
                .ToArray());

            var discriminators = store.Database.RawQuery<(string, string)>(builder.ToString(), builder.Parameters, schema: true, commandTimeout: 300);

            foreach (var (table, discriminator) in discriminators)
            {
                var design = store.Configuration.GetDesignByDiscriminator(discriminator);

                if (design != null)
                {
                    if (design.Table.Name != table)
                    {
                        throw new InvalidOperationException(Indent($@"
                            Found a document with discriminator '{discriminator}' in table '{table}', 
                            but it is configured to be in '{design.Table.Name}'.

                            You must change the configuration or migrate the database so they match each other."));
                    }

                    continue;
                }

                logger.LogInformation(Indent($@"
                    HybridDb startup: Found one or more documents in table '{table}' with discriminator '{discriminator}', 
                    which is not configured. They will be configured automatically."));

                var type = store.Configuration.TypeMapper.ToType(typeof(object), discriminator);
                var newDesign = store.Configuration.GetOrCreateDesignFor(type, table, discriminator);

                logger.LogInformation(Indent($@"
                    HybridDb startup: Autoconfiguring documents with discriminator '{discriminator}' to match type '{type}'
                    with base type '{newDesign.Root.DocumentType}' which reside in table '{newDesign.Table.Name}'."));
            }
        }

        IReadOnlyList<(string Table, SqlBuilder Where)> FindPredicatesForBackgroundMigrations(int oldSchemaVersion)
        {
            var result = new List<(string Table, SqlBuilder Where)>();

            foreach (var migration in migrations)
            {
                if (migration.Version <= oldSchemaVersion) continue;

                foreach (var rowMigrationCommand in migration.Background(store.Configuration))
                {
                    result.Add((
                        rowMigrationCommand.GetTable(store.Configuration),
                        rowMigrationCommand.GetMatches(store, migration.Version)));
                }
            }

            return result;
        }

        void TryCreateMetadataTable()
        {
            logger.LogInformation("HybridDb startup: Creating metadata table if needed.");

            var metadata = store.Configuration.GetMetadataTable();

            store.Execute(new CreateTable(metadata));

            var hybridDbTableName = store.Database.FormatTableNameAndEscape(metadata.Name);

            var result = store.Database.RawExecute($@"
                if not exists (select * from {hybridDbTableName})
                    insert into {hybridDbTableName} (SchemaVersion) values (-1);", 
                schema: true);

            logger.LogInformation(result == 0
                ? "HybridDb startup: Metadata table already exists."
                : "HybridDb startup: Metadata table created.");
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
                logger.LogInformation($"HybridDb startup: Found no configured migrations.");

                return GetReprojections(RunAutoMigrations(schemaVersion, hasMigrationsBeforeAutoMigrations: false));
            }

            logger.LogInformation("HybridDb startup: Found configured migrations from version {0} to {1}.", schemaVersion, store.Configuration.ConfiguredVersion);

            var requiresReprojection = new List<string>();

            logger.LogInformation("HybridDb startup: Running migrations confirugred for before automigrations.");

            var beforeCommands = RunConfiguredMigrations(configuredMigrations, x => x.BeforeAutoMigrations(store.Configuration)).ToList();
            requiresReprojection.AddRange(GetReprojections(beforeCommands));

            requiresReprojection.AddRange(GetReprojections(RunAutoMigrations(schemaVersion, hasMigrationsBeforeAutoMigrations: beforeCommands.Any())));

            logger.LogInformation("HybridDb startup: Running migrations configured for after automigrations.");

            var afterCommands = RunConfiguredMigrations(configuredMigrations, x => x.AfterAutoMigrations(store.Configuration));
            requiresReprojection.AddRange(GetReprojections(afterCommands));

            logger.LogInformation("HybridDb startup: Schema is migrated from version {0} to {1}.", schemaVersion, store.Configuration.ConfiguredVersion);

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

            logger.LogInformation("HybridDb startup: Looking for differences between database schema and configuration.");

            var commands = differ.CalculateSchemaChanges(schema, store.Configuration);

            if (!commands.Any())
            {
                logger.LogInformation("HybridDb startup: Database schema matches configuration. No automigrations will be run.");

                return new List<DdlCommand>();
            }

            logger.LogInformation("HybridDb startup: Found {0} differences between database schema and configuration. Will automigrate database to get up to date.", commands.Count);

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
                logger.LogInformation("HybridDb startup: Skips configured migrations when not using real tables.");
                return new List<Migration>();
            }

            if (schemaVersion >= store.Configuration.ConfiguredVersion) return new List<Migration>();

            return migrations
                .OrderBy(x => x.Version)
                .Where(x => x.Version > schemaVersion)
                .ToList();
        }

        void MarkDocumentsForBackgroundMigration(IEnumerable<(string Table, SqlBuilder Where)> needsMigration)
        {
            foreach (var grouping in needsMigration.GroupBy(x => x.Table))
            {
                var tablename = grouping.Key;

                var design = store.Configuration.TryGetDesignByTablename(tablename);
                if (design == null) continue;

                var sql = new SqlBuilder()
                    .Append(@$"
                        update {store.Database.FormatTableNameAndEscape(tablename)}
                        set AwaitsMigration=@AwaitsMigration",
                        new SqlParameter("AwaitsMigration", true))
                    .Append("where")
                    .Append(grouping
                        .Select((value, index) => (value.Where, Index: index))
                        .Aggregate(new SqlBuilder(), (current, x) => current
                            .Append(x.Index == 0, "(", "or (")
                            .Append(x.Where)
                            .Append(")")));

                var count = store.Database.RawExecute(sql.ToString(), sql.Parameters, schema: true, commandTimeout: 300);

                logger.LogInformation($"HybridDb startup: Marked {count} documents for background migration in '{tablename}'.");
            }
        }

        IEnumerable<DdlCommand> ExecuteCommand(DdlCommand command, bool allowUnsafe)
        {
            if (!command.Safe && !allowUnsafe)
            {
                logger.LogWarning("HybridDb startup: Unsafe migration command '{0}' was skipped.", command.ToString());
                yield break;
            }

            logger.LogInformation("HybridDb startup: Executing migration command '{0}'.", command.ToString());

            store.Execute(command);

            yield return command;
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
                    using var tx = BeginTransaction();

                    LockDatabase();

                    action();

                    tx.Complete();
                }
            }

            logger.LogInformation("HybridDb startup: Upfront migrations ran in {time}ms on {tables}.", sw.ElapsedMilliseconds, isTempTables ? "temp tables" : "real tables");
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
            using var connection = store.Database.Connect(true);

            var parameters = new DynamicParameters();
            parameters.Add("@Resource", $"HybridDb");
            parameters.Add("@DbPrincipal", "public");
            parameters.Add("@LockMode", "Exclusive");
            parameters.Add("@LockOwner", "Transaction");
            parameters.Add("@LockTimeout", TimeSpan.FromSeconds(300).TotalMilliseconds);
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
}