using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Transactions;
using Dapper;
using HybridDb.Logging;
using Shouldly;

namespace HybridDb.Tests
{
    public abstract class HybridDbDatabaseTests : HybridDbConfigurator, IDisposable
    {
        readonly ConsoleLogger logger;
        readonly List<Action> disposables;
        
        protected string connectionString;
        protected Database database;

        protected HybridDbDatabaseTests()
        {
            logger = new ConsoleLogger(LogLevel.Debug, new LoggingColors());
            disposables = new List<Action>();

            UseTempTables();
        }

        protected void Use(TableMode mode)
        {
            switch (mode)
            {
                case TableMode.UseRealTables:
                    UseRealTables();
                    break;
                case TableMode.UseTempTables:
                    UseTempTables();
                    break;
                case TableMode.UseGlobalTempTables:
                    throw new Exception();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("mode");
            }
        }

        protected void UseTempTables()
        {
            connectionString = "data source=.;Integrated Security=True";
            database = Using(new Database(logger, connectionString, TableMode.UseTempTables, testMode: true));
        }

        protected void UseRealTables()
        {
            var uniqueDbName = "HybridDbTests_" + Guid.NewGuid().ToString().Replace("-", "_");
            using (var connection = new SqlConnection("data source=.;Integrated Security=True;Pooling=false"))
            {
                connection.Open();

                connection.Execute(String.Format(@"
                        IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{0}')
                        BEGIN
                            CREATE DATABASE {0}
                        END", uniqueDbName));
            }

            connectionString = "data source=.;Integrated Security=True;Initial Catalog=" + uniqueDbName;

            database = Using(new Database(logger, connectionString, TableMode.UseRealTables, testMode: true));

            disposables.Add(() =>
            {
                SqlConnection.ClearAllPools();

                using (var connection = new SqlConnection("data source=.;Integrated Security=True;Initial Catalog=Master"))
                {
                    connection.Open();
                    connection.Execute(String.Format("DROP DATABASE {0}", uniqueDbName));
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
                TheSecondChild = new Child();
                Children = new List<Child>();
            }

            public Guid Id { get; set; }
            public string ProjectedProperty { get; set; }
            public List<Child> Children { get; set; }
            public string Property { get; set; }
            public int Number { get; set; }
            public Child TheChild { get; set; }
            public Child TheSecondChild { get; set; }

            public class Child
            {
                public string NestedProperty { get; set; }
            }
        }

        public class OtherEntity
        {
            public Guid Id { get; set; }
            public int Number { get; set; }
        }

        public abstract class AbstractEntity : ISomeInterface
        {
            public Guid Id { get; set; }
            public string Property { get; set; }
            public int Number { get; set; }
        }

        public class DerivedEntity : AbstractEntity { }
        public class MoreDerivedEntity1 : DerivedEntity, IOtherInterface { }
        public class MoreDerivedEntity2 : DerivedEntity { }

    }
}