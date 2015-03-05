using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Transactions;
using Dapper;
using Shouldly;

namespace HybridDb.Tests
{
    public abstract class HybridDbTests : HybridDbConfigurator, IDisposable
    {
        readonly List<Action> disposables;
        
        string uniqueDbName;
        Lazy<DocumentStore> factory;

        protected HybridDbTests()
        {
            disposables = new List<Action>();

            UseTempTables();
            UseSerializer(new DefaultJsonSerializer());
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
            if (factory != null && factory.IsValueCreated)
                throw new InvalidOperationException("Cannot change table mode when store is already initialized.");

            factory = new Lazy<DocumentStore>(() => Using(DocumentStore.ForTestingWithTempTables(configurator: this)));
        }

        protected void UseRealTables()
        {
            if (factory != null && factory.IsValueCreated)
                throw new InvalidOperationException("Cannot change table mode when store is already initialized.");

            uniqueDbName = "HybridDbTests_" + Guid.NewGuid().ToString().Replace("-", "_");
            factory = new Lazy<DocumentStore>(() =>
            {
                using (var connection = new SqlConnection("data source=.;Integrated Security=True;Pooling=false"))
                {
                    connection.Open();

                    connection.Execute(String.Format(@"
                        IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{0}')
                        BEGIN
                            CREATE DATABASE {0}
                        END", uniqueDbName));
                }

                var realTableStore = Using(DocumentStore.ForTestingWithRealTables("data source=.;Integrated Security=True;Initial Catalog=" + uniqueDbName, this));

                disposables.Add(() =>
                {
                    SqlConnection.ClearAllPools();

                    using (var connection = new SqlConnection("data source=.;Integrated Security=True;Initial Catalog=Master"))
                    {
                        connection.Open();
                        connection.Execute(String.Format("DROP DATABASE {0}", uniqueDbName));
                    }
                });

                return realTableStore;
            });
        }

        protected void EnsureStoreInitialized()
        {
            // touch the store to have it initialized
            var touch = store;
        }

        protected void ResetStore()
        {
            
        }

        // ReSharper disable InconsistentNaming
        protected DocumentStore store
        {
            get { return factory.Value; }
        }
        // ReSharper restore InconsistentNaming

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