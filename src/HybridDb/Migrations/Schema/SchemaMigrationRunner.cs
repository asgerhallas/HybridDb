using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Microsoft.Extensions.Logging;
using static Indentional.Indent;
using IsolationLevel = System.Transactions.IsolationLevel;

namespace HybridDb.Migrations.Schema
{
    public class SchemaMigrationRunner
    {
        static readonly object locker = new();

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
            if (!store.Configuration.RunUpfrontMigrations)
                return;
            
            Migrate(store.TableMode == TableMode.GlobalTempTables, () =>
            {
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
                    using var tx = BeginTransaction();
                    
                    LockDatabase();

                    action();

                    tx.Complete();
                }
            }

            logger.LogInformation($"Schema migrations ran in {{time}}ms on {{tables}}.", sw.ElapsedMilliseconds, isTempTables ? "temp tables" : "real tables");
        }

        static TransactionScope BeginTransaction() => new(
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

            if (!commands.Any()) return Enumerable.Empty<string>();

            logger.LogInformation("Found {0} differences between current schema and configuration. Migrates schema to get up to date.", commands.Count);

            return commands.SelectMany(command => ExecuteCommand(command, false), (_, tablename) => tablename);
        }

        IEnumerable<string> RunConfiguredMigrations(int schemaVersion)
        {
            if (!(store.Database is SqlServerUsingRealTables))
            {
                logger.LogInformation("Skips provided migrations when not using real tables.");
                yield break;
            }

            if (schemaVersion >= store.Configuration.ConfiguredVersion) yield break;

            var migrationsToRun = migrations.OrderBy(x => x.Version).Where(x => x.Version > schemaVersion).ToList();

            logger.LogInformation("Migrates schema from version {0} to {1}.", schemaVersion, store.Configuration.ConfiguredVersion);

            foreach (var migration in migrationsToRun)
            {
                foreach (var command in migration.Upfront(store.Configuration))
                {
                    foreach (var tablename in ExecuteCommand(command, true))
                    {
                        yield return tablename;
                    }
                }
            }
        }

        void MarkDocumentsForReprojections(IEnumerable<string> requiresReprojection)
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
            if (!command.Safe && !allowUnsafe)
            {
                logger.LogWarning("Unsafe migration command '{0}' was skipped.", command.ToString());
                yield break;
            }

            logger.LogInformation("Executing migration command '{0}'.", command.ToString());

            store.Execute(command);

            if (command.RequiresReprojectionOf != null)
                yield return command.RequiresReprojectionOf;
        }
    }
}