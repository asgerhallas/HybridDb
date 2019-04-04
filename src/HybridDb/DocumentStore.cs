using System;
using HybridDb.Config;
using HybridDb.Migrations;
using Serilog;
using IsolationLevel = System.Data.IsolationLevel;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        internal DocumentStore(Configuration configuration, TableMode mode, string connectionString, bool testing)
        {
            Configuration = configuration;
            Logger = configuration.Logger;
            Testing = testing;
            TableMode = mode;

            switch (mode)
            {
                case TableMode.UseRealTables:
                    Database = new SqlServerUsingRealTables(this, connectionString);
                    break;
                case TableMode.UseLocalTempTables:
                    Database = new SqlServerUsingLocalTempTables(this, connectionString);
                    break;
                case TableMode.UseGlobalTempTables:
                    Database = new SqlServerUsingGlobalTempTables(this, connectionString);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        internal DocumentStore(DocumentStore store, Configuration configuration)
        {
            Configuration = configuration;
            Logger = configuration.Logger;
            Testing = store.Testing;
            TableMode = store.TableMode;
            Database = store.Database;
        }

        public static IDocumentStore Create(string connectionString, Action<Configuration> configure = null)
        {
            configure = configure ?? (x => { });
            var configuration = new Configuration();
            configure(configuration);
            return new DocumentStore(configuration, TableMode.UseRealTables, connectionString, false);
        }

        public static IDocumentStore ForTesting(TableMode mode, Action<Configuration> configure = null) => ForTesting(mode, null, configure);

        public static IDocumentStore ForTesting(TableMode mode, Configuration configuration) => ForTesting(mode, null, configuration);

        public static IDocumentStore ForTesting(TableMode mode, string connectionString, Action<Configuration> configure = null)
        {
            configure = configure ?? (x => { });
            var configuration = new Configuration();
            configure(configuration);
            return ForTesting(mode, connectionString, configuration);
        }

        public static IDocumentStore ForTesting(TableMode mode, string connectionString, Configuration configuration) => 
            new DocumentStore(configuration, mode, connectionString ?? "data source=.;Integrated Security=True", true);

        public void Dispose() => Database.Dispose();

        public IDatabase Database { get; }
        public ILogger Logger { get; private set; }
        public Configuration Configuration { get; }
        public bool IsInitialized { get; private set; }
        public bool Testing { get; }
        public TableMode TableMode { get; }

        public StoreStats Stats { get; } = new StoreStats();

        public void Initialize()
        {
            if (IsInitialized)
                return;

            Configuration.Initialize();
            Database.Initialize();

            Logger = Configuration.Logger;

            new SchemaMigrationRunner(this, new SchemaDiffer()).Run();
            var documentMigration = new DocumentMigrationRunner().Run(this);
            if (Testing) documentMigration.Wait();

            IsInitialized = true;
        }

        public IDocumentSession OpenSession(IDocumentTransaction tx = null)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("You must call Initialize() on the store before opening a session.");
            }

            return new DocumentSession(this, tx);
        }

        public IDocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted) => new DocumentTransaction(this, level, Stats);
    }
}