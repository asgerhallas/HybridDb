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

            Configuration.Initialize();
            Database.Initialize();

            if (initialize) Initialize();
        }

        internal DocumentStore(DocumentStore store, Configuration configuration)
        {
            Configuration = configuration;
            Logger = configuration.Logger;
            TableMode = store.TableMode;
            Database = store.Database;

            Configuration.Initialize();
            Initialize();
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

            return ForTesting(mode, configuration);
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

            new SchemaMigrationRunner(this, new SchemaDiffer()).Run();
            DocumentMigration = new DocumentMigrationRunner().Run(this);

            IsInitialized = true;
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
            AssertInitialized();

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