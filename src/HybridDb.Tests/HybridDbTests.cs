using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Transactions;
using Dapper;
using HybridDb.Migrations;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Events;
using Shouldly;

namespace HybridDb.Tests
{
    public abstract class HybridDbTests : HybridDbConfigurator, IDisposable
    {
        readonly List<Action> disposables;

        protected readonly ILogger logger;
        
        protected string connectionString;
        protected IDatabase database;

        protected HybridDbTests()
        {
            logger = new LoggerConfiguration()
                .MinimumLevel.Is(Debugger.IsAttached ? LogEventLevel.Debug : LogEventLevel.Information)
                .WriteTo.ColoredConsole()
                .CreateLogger();
            
            disposables = new List<Action>();

            UseTempTables();
        }

        protected void Use(TableMode mode, string prefix = null)
        {
            switch (mode)
            {
                case TableMode.UseRealTables:
                    UseRealTables(prefix);
                    break;
                case TableMode.UseTempTables:
                    UseTempTables();
                    break;
                case TableMode.UseTempDb:
                    UseTempDb(prefix);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("mode");
            }
        }

        protected void UseTempTables()
        {
            Console.WriteLine("HEJ ASGER:");
            Console.WriteLine(Environment.GetEnvironmentVariable("APPVEYOR"));

            connectionString = GetConnectionString();
            database = Using(new SqlServerUsingTempTables(logger, connectionString));
        }

        protected void UseTempDb(string sessionKey = null)
        {
            connectionString = GetConnectionString();
            database = Using(new SqlServerUsingTempDb(logger, connectionString, sessionKey));
        }

        protected void UseRealTables(string prefix = null)
        {
            var uniqueDbName = "HybridDbTests_" + Guid.NewGuid().ToString().Replace("-", "_");

            using (var connection = new SqlConnection(GetConnectionString() + ";Pooling=false"))
            {
                connection.Open();

                connection.Execute(String.Format(@"
                        IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{0}')
                        BEGIN
                            CREATE DATABASE {0}
                        END", uniqueDbName));
            }

            connectionString = GetConnectionString() + ";Initial Catalog=" + uniqueDbName;

            database = Using(new SqlServerUsingRealTables(logger, connectionString, prefix));

            disposables.Add(() =>
            {
                SqlConnection.ClearAllPools();

                using (var connection = new SqlConnection(GetConnectionString() + ";Initial Catalog=Master"))
                {
                    connection.Open();
                    connection.Execute($"DROP DATABASE {uniqueDbName}");
                }
            });
        }

        protected T Using<T>(T disposable) where T : IDisposable
        {
            disposables.Add(disposable.Dispose);
            return disposable;
        }

        public void Dispose()
        {
            foreach (var dispose in disposables)
            {
                dispose();
            }

            Transaction.Current.ShouldBe(null);
        }

        static string GetConnectionString()
        {
            var isAppveyor = Environment.GetEnvironmentVariable("APPVEYOR") != null;

            return isAppveyor
                ? "Server=(local)\\SQL2012SP1;Database=master;User ID=sa;Password=Password12!"
                : "data source =.; Integrated Security = True;";
        }

        public interface ISomeInterface
        {
            string Property { get; }
        }

        public interface IOtherInterface
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