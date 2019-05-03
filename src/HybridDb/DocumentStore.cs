using System;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Documents;
using HybridDb.Migrations.Schema;
using Serilog;
using IsolationLevel = System.Data.IsolationLevel;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        internal DocumentStore(TableMode mode, Configuration configuration)
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

            new SchemaMigrationRunner(this, new SchemaDiffer()).Run();
            DocumentMigration = new DocumentMigrationRunner().Run(this);
        }

        internal DocumentStore(DocumentStore store, Configuration configuration)
        {
            Configuration = configuration;
            Logger = configuration.Logger;
            TableMode = store.TableMode;
            Database = store.Database;

            Configuration.Initialize();

            new SchemaMigrationRunner(this, new SchemaDiffer()).Run();
            DocumentMigration = new DocumentMigrationRunner().Run(this);
        }

        public static IDocumentStore Create(Action<Configuration> configure)
        {
            var configuration = new Configuration();

            configure(configuration); 

            return new DocumentStore(TableMode.RealTables, configuration);
        }

        public static IDocumentStore Create(Configuration configuration = null) => new DocumentStore(TableMode.RealTables, configuration ?? new Configuration());

        public static IDocumentStore ForTesting(TableMode mode, Action<Configuration> configure)
        {
            var configuration = new Configuration();

            configure(configuration);

            return ForTesting(mode, configuration);
        }

        public static IDocumentStore ForTesting(TableMode mode, Configuration configuration = null) => new DocumentStore(mode, configuration ?? new Configuration());

        public void Dispose() => Database.Dispose();

        public IDatabase Database { get; }
        public ILogger Logger { get; private set; }
        public Configuration Configuration { get; }
        public TableMode TableMode { get; }
        public Task DocumentMigration { get; }

        public StoreStats Stats { get; } = new StoreStats();

        public IDocumentSession OpenSession(DocumentTransaction tx = null) => new DocumentSession(this, tx);

        public DocumentTransaction BeginTransaction(Guid commitId, IsolationLevel level = IsolationLevel.ReadCommitted) => new DocumentTransaction(this, commitId, level, Stats);
        public DocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted) => BeginTransaction(Guid.NewGuid(), level);

        public void Execute(DdlCommand command) => Configuration.GetDdlCommandExecutor(this, command)();
        public object Execute(DocumentTransaction tx, DmlCommand command) => Configuration.GetDmlCommandExecutor(tx, command)();
        public T Execute<T>(DocumentTransaction tx, Command<T> command) => (T)Configuration.GetDmlCommandExecutor(tx, command)();

   }
}