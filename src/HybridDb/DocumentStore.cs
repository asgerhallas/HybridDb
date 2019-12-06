using System;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migrations.Documents;
using HybridDb.Migrations.Schema;
using Serilog;
using IsolationLevel = System.Data.IsolationLevel;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        internal DocumentStore(TableMode mode, Configuration configuration, bool initialize)
        {
            Configuration = configuration;
            Logger = configuration.Logger;
            TableMode = mode;

            switch (mode)
            {
                case TableMode.RealTables:
                    Database = new SqlServerUsingRealTables(this, configuration.ConnectionString);
                    break;
                case TableMode.GlobalTempTables:
                    Database = new SqlServerUsingGlobalTempTables(this, configuration.ConnectionString + ";Initial Catalog=TempDb");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            if (initialize) Initialize();
        }

        internal DocumentStore(DocumentStore store, Configuration configuration, bool initialize)
        {
            Configuration = configuration;
            Logger = configuration.Logger;
            TableMode = store.TableMode;
            Database = store.Database;

            if (initialize) Initialize();
        }

        public static DocumentStore Create(Action<Configuration> configure, bool initialize = true)
        {
            var configuration = new Configuration();

            configure(configuration);

            return new DocumentStore(TableMode.RealTables, configuration, initialize);
        }

        public static DocumentStore Create(Configuration configuration = null, bool initialize = true) => 
            new DocumentStore(TableMode.RealTables, configuration ?? new Configuration(), initialize);

        public static DocumentStore ForTesting(TableMode mode, Action<Configuration> configure, bool initialize = true)
        {
            var configuration = new Configuration();

            configure(configuration);

            return ForTesting(mode, configuration, initialize);
        }

        public static DocumentStore ForTesting(TableMode mode, Configuration configuration = null, bool initialize = true) => 
            new DocumentStore(mode, configuration ?? new Configuration(), initialize);

        public void Dispose() => Database.Dispose();

        public IDatabase Database { get; }
        public ILogger Logger { get;  }
        public Configuration Configuration { get; }
        public TableMode TableMode { get; }
        public StoreStats Stats { get; } = new StoreStats();

        public bool IsInitialized { get; private set; }
        public Task DocumentMigration { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) throw new InvalidOperationException("Store is already initialized.");

            Configuration.Initialize();
            Database.Initialize();

            // No use of the store for handling documents is allowed before this is run.
            new SchemaMigrationRunner(this, new SchemaDiffer()).Run();

            // Set as initialized before invoking document migrations, to avoid errors when the runner
            // tries to open sessions and execute commands. Concurrent use of the store for handling
            // documents is permitted from this time on.
            IsInitialized = true;

            DocumentMigration = new DocumentMigrationRunner().Run(this);
        }

        public IDocumentSession OpenSession(DocumentTransaction tx = null)
        {
            AssertInitialized();

            return new DocumentSession(this, tx);
        }

        public DocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted) => BeginTransaction(Guid.NewGuid(), level);
        public DocumentTransaction BeginTransaction(Guid commitId, IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            AssertInitialized();

            return new DocumentTransaction(this, commitId, level, Stats);
        }

        public void Execute(DdlCommand command)
        {
            if (IsInitialized) throw new InvalidOperationException("Changing database schema is not allowed after store initialization.");

            Configuration.GetDdlCommandExecutor(this, command)();
        }

        public object Execute(DocumentTransaction tx, DmlCommand command)
        {
            AssertInitialized();

            return Configuration.GetDmlCommandExecutor(tx, command)();
        }

        public T Execute<T>(DocumentTransaction tx, Command<T> command)
        {
            AssertInitialized();

            return (T) Configuration.GetDmlCommandExecutor(tx, command)();
        }

        void AssertInitialized()
        {
            if (!IsInitialized) throw new InvalidOperationException("Store is not initialized. Please call Initialize().");
        }
   }
}