using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using HybridDb.Migrations.Documents;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Events;
using Shouldly;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public abstract class HybridDbTests : HybridDbConfigurator, IDisposable
    {
        readonly ConcurrentStack<Action> disposables;

        protected readonly ITestOutputHelper output;
        protected readonly List<LogEvent> log = new List<LogEvent>();
        protected readonly ILogger logger;
        protected string connectionString;
        bool autoInitialize = true;

        Lazy<DocumentStore> activeStore;

        protected HybridDbTests(ITestOutputHelper output)
        {
            this.output = output;

            logger = new LoggerConfiguration()
                .MinimumLevel.Is(Debugger.IsAttached ? LogEventLevel.Debug : LogEventLevel.Information)
                .WriteTo.ColoredConsole()
                .WriteTo.Sink(new ListSink(log))
                .CreateLogger();

            UseLogger(logger);

            disposables = new ConcurrentStack<Action>();

            UseGlobalTempTables();
        }

        protected DocumentStore store
        {
            get
            {
                var s = activeStore.Value;

                if (s.IsInitialized)
                {
                    s.DocumentMigration.Wait();
                }

                return s;
            }
        }

        protected static string GetConnectionString()
        {
            var isAppveyor = Environment.GetEnvironmentVariable("APPVEYOR") != null;

            return isAppveyor
                ? "Server=(local)\\SQL2014;Database=master;User ID=sa;Password=Password12!"
                : "data source =.; Integrated Security = True";
        }

        protected void NoInitialize() => autoInitialize = false;

        protected void Use(TableMode mode, string prefix = null)
        {
            switch (mode)
            {
                case TableMode.RealTables:
                    UseRealTables();
                    break;
                case TableMode.GlobalTempTables:
                    UseGlobalTempTables();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        protected void UseGlobalTempTables()
        {
            connectionString = GetConnectionString();

            configuration.UseConnectionString(connectionString);

            activeStore = new Lazy<DocumentStore>(() => Using(new DocumentStore(TableMode.GlobalTempTables, configuration, autoInitialize)));
        }

        protected void UseRealTables()
        {
            var uniqueDbName = $"HybridDbTests_{Guid.NewGuid().ToString().Replace("-", "_")}";

            output.WriteLine("Database name: " + uniqueDbName);

            using (var connection = new SqlConnection(GetConnectionString() + ";Pooling=false"))
            {
                connection.Open();

                connection.Execute(string.Format(@"
                        IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{0}')
                        BEGIN
                            CREATE DATABASE {0}
                        END", uniqueDbName));
            }

            using (var connection = new SqlConnection(GetConnectionString() + ";Pooling=false"))
            {
                connection.Open();

                connection.Execute($"ALTER DATABASE {uniqueDbName} SET ALLOW_SNAPSHOT_ISOLATION ON;");
            }

            connectionString = GetConnectionString() + ";Initial Catalog=" + uniqueDbName;

            configuration.UseConnectionString(connectionString);
            
            disposables.Push(() =>
            {
                SqlConnection.ClearAllPools();

                using (var connection = new SqlConnection(GetConnectionString() + ";Initial Catalog=Master"))
                {
                    connection.Open();
                    connection.Execute($"DROP DATABASE {uniqueDbName}");
                }
            });

            activeStore = new Lazy<DocumentStore>(() => Using(new DocumentStore(TableMode.RealTables, configuration, autoInitialize)));
        }

        protected void InitializeStore()
        {
            var _ = store;
        }

        /// <summary>
        /// Resets configuration, but not the database. Can be used to test new configurations against existing data.
        /// </summary>
        protected void ResetConfiguration()
        {
            var currentStore = store;

            configuration = new Configuration();

            configuration.UseConnectionString(connectionString);
            configuration.UseLogger(logger);

            activeStore = new Lazy<DocumentStore>(() => Using(new DocumentStore(currentStore, configuration, autoInitialize))); 
        }

        /// <summary>
        /// Resets store, but not configuration or database. Can be used to test a new store with same configuration and database.
        /// </summary>
        protected void ResetStore()
        {
            var currentStore = store;

            activeStore = new Lazy<DocumentStore>(() => Using(new DocumentStore(currentStore, configuration, autoInitialize))); 
        }

        protected T Using<T>(T disposable) where T : IDisposable
        {
            disposables.Push(disposable.Dispose);
            return disposable;
        }

        protected string NewId() => Guid.NewGuid().ToString();

        public void Dispose()
        {
            while (disposables.TryPop(out var dispose))
            {
                dispose();
            }

            Transaction.Current.ShouldBe(null);
        }

        public interface ISomeInterface
        {
            string Property { get; }
        }

        public interface IOtherInterface
        {
            string Property { get; }
        }

        public interface IUnusedInterface
        {
        }

        public class Entity : ISomeInterface
        {
            public Entity()
            {
                TheChild = new Child();
                Children = new List<Child>();
            }

            public string Id { get; set; }
            public string ProjectedProperty { get; set; }
            public List<Child> Children { get; set; }
            public string Field;
            public string Property { get; set; }
            public int Number { get; set; }
            public DateTime DateTimeProp { get; set; }
            public SomeFreakingEnum EnumProp { get; set; }
            public Child TheChild { get; set; }
            public ComplexType Complex { get; set; }

            public class Child
            {
                public string NestedProperty { get; set; }
                public double NestedDouble { get; set; }
            }

            public class ComplexType
            {
                public string A { get; set; }
                public int B { get; set; }

                public override string ToString()
                {
                    return A + B;
                }
            }
        }

        public class OtherEntity
        {
            public string Id { get; set; }
            public int Number { get; set; }
        }

        public abstract class AbstractEntity : ISomeInterface
        {
            public string Id { get; set; }
            public string Property { get; set; }
            public int Number { get; set; }
        }

        public class DerivedEntity : AbstractEntity { }
        public class MoreDerivedEntity1 : DerivedEntity, IOtherInterface { }
        public class MoreDerivedEntity2 : DerivedEntity { }

        public enum SomeFreakingEnum
        {
            One,
            Two
        }

        public class ChangeDocumentAsJObject<T> : ChangeDocument<T>
        {
            public ChangeDocumentAsJObject(Action<JObject> change)
                : base((session, serializer, json) =>
                {
                    var jObject = (JObject)serializer.Deserialize(json, typeof(JObject));
                    
                    change(jObject);
                    
                    return serializer.Serialize(jObject);
                })
            {
            }
        }
    }
}