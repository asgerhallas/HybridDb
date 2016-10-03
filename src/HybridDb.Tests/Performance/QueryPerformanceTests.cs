using System;
using System.Collections.Generic;
using HybridDb.Commands;
using Shouldly;
using Xunit;
using System.Linq;

namespace HybridDb.Tests.Performance
{
    public class QueryPerformanceTests : PerformanceTests, IUseFixture<QueryPerformanceTests.Fixture>
    {
        protected IDocumentStore store;

        [Fact]
        public void SimpleQueryWithoutMaterialization()
        {
            Time((out QueryStats stats) => store.Query(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats))
                .DbTimeLowest.ShouldBeLessThan(40);
        }

        [Fact]
        public void SimpleQueryWithMaterializationToDictionary()
        {
            Time((out QueryStats stats) => store.Query(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats).ToList())
                .CodeTimeLowest.ShouldBeLessThan(40);
        }

        [Fact]
        public void SimpleQueryWithMaterializationToProjection()
        {
            Time((out QueryStats stats) => store.Query<LocalEntity>(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats).ToList())
                .CodeTimeLowest.ShouldBeLessThan(5);
        }

        [Fact]
        public void QueryWithWindow()
        {
            Time((out QueryStats stats) => store.Query(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats, skip: 200, take: 500))
                .DbTimeLowest.ShouldBeLessThan(3);
        }

        [Fact]
        public void QueryWithLateWindow()
        {
            Time((out QueryStats stats) => store.Query(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats, skip: 9500, take: 500))
                .DbTimeLowest.ShouldBeLessThan(4);
        }

        [Fact]
        public void QueryWithWindowMaterializedToProjection()
        {
            Time((out QueryStats stats) => store.Query<LocalEntity>(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats, skip: 200, take: 500).ToList())
                .TotalTimeLowest.ShouldBeLessThan(20);
        }

        public void SetFixture(Fixture data)
        {
            store = data.Store;
        }

        public class Fixture : HybridDbTests
        {
            readonly IDocumentStore store;

            public IDocumentStore Store
            {
                get { return store; }
            }

            public Fixture()
            {
                store = Using(DocumentStore.ForTesting(
                    TableMode.UseTempTables,
                    connectionString,
                    c => c.Document<LocalEntity>()
                        .With(x => x.SomeData)
                        .With(x => x.SomeNumber)));

                store.Initialize();

                var commands = new List<DatabaseCommand>();
                for (int i = 0; i < 10000; i++)
                {
                    commands.Add(new InsertCommand(
                        store.Configuration.GetDesignFor<LocalEntity>().Table,
                        Guid.NewGuid().ToString(),
                        new {SomeNumber = i, SomeData = "ABC"}));
                }

                store.Execute(commands);
            }
        }
    }
}