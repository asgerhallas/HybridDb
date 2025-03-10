using System;
using System.Reflection;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migrations.Documents;
using HybridDb.Migrations.Schema;
using Microsoft.Extensions.Logging;
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
            DocumentMigrationRunner = new DocumentMigrationRunner(this);

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

            Configuration.TypeMapper.Add(Assembly.GetCallingAssembly());

            if (initialize) Initialize();
        }

        public DocumentStore(DocumentStore store, Configuration configuration, bool initialize)
        {
            Configuration = configuration;
            Logger = configuration.Logger;
            TableMode = store.TableMode;
            DocumentMigrationRunner = new DocumentMigrationRunner(this);
            Database = store.Database;

            Configuration.TypeMapper.Add(Assembly.GetCallingAssembly());

            if (initialize) Initialize();
        }

        public static DocumentStore Create(Action<Configuration> configure, bool initialize = true)
        {
            var configuration = new Configuration();

            configure(configuration);

            return new DocumentStore(TableMode.RealTables, configuration, initialize);
        }

        public static DocumentStore Create(Configuration configuration = null, bool initialize = true) => 
            new(TableMode.RealTables, configuration ?? new Configuration(), initialize);

        public static DocumentStore ForTesting(TableMode mode, Action<Configuration> configure, bool initialize = true)
        {
            var configuration = new Configuration();

            configure(configuration);

            return ForTesting(mode, configuration, initialize);
        }

        public static DocumentStore ForTesting(TableMode mode, Configuration configuration = null, bool initialize = true) => 
            new(mode, configuration ?? new Configuration(), initialize);

        public void Dispose()
        {
            DocumentMigrationRunner.Dispose();
            Configuration.Dispose();
            Database.Dispose();
        }

        public IDatabase Database { get; }
        public ILogger Logger { get;  }
        public Configuration Configuration { get; }
        public TableMode TableMode { get; }
        public StoreStats Stats { get; } = new();
        public DocumentMigrationRunner DocumentMigrationRunner { get; }

        public bool IsInitialized { get; private set; }
        public DocumentMigrator Migrator { get; private set; }

        public Task DocumentMigration { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) throw new InvalidOperationException("Store is already initialized.");

            // No use of the store for handling documents is allowed before this is run.
            // The SchemaMigrationRunner will initialize the Database and initialize/freeze the configuration.
            new SchemaMigrationRunner(this, new SchemaDiffer()).Run();
            
            Migrator = Configuration.Resolve<DocumentMigrator>();

            // Set as initialized before invoking document migrations, to avoid errors when the runner
            // tries to open sessions and execute commands. Concurrent use of the store for handling
            // documents is permitted from this time on.
            IsInitialized = true;

            DocumentMigration = DocumentMigrationRunner.Run();
        }

        public IDocumentSession OpenSession(DocumentTransaction tx = null)
        {
            AssertInitialized();

            return new DocumentSession(this, Migrator, tx);
        }

        public DocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted, TimeSpan? connectionTimeout = null) => BeginTransaction(Guid.NewGuid(), level, connectionTimeout);
        public DocumentTransaction BeginTransaction(Guid commitId, IsolationLevel level = IsolationLevel.ReadCommitted, TimeSpan? connectionTimeout = null)
        {
            AssertInitialized();

            return new DocumentTransaction(this, commitId, level, Stats, connectionTimeout);
        }

        public void Execute(DdlCommand command)
        {
            if (IsInitialized) throw new InvalidOperationException("Changing database schema is not allowed after store initialization.");

            Configuration.Resolve<DdlCommandExecutor>()(this, command);
        }

        public object Execute(DocumentTransaction tx, DmlCommand command)
        {
            AssertInitialized();

            return Configuration.Resolve<DmlCommandExecutor>()(tx, command);
        }

        public T Execute<T>(DocumentTransaction tx, Command<T> command)
        {
            AssertInitialized();

            return (T) Configuration.Resolve<DmlCommandExecutor>()(tx, command);
        }

        void AssertInitialized()
        {
            if (!IsInitialized) throw new InvalidOperationException("Store is not initialized. Please call Initialize().");
        }
   }
}