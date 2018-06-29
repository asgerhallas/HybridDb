using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                .DbTime.ShouldBeLessThan(3600);
        }

        [Fact]
        public void SimpleQueryWithMaterializationToDictionary()
        {
            Time((out QueryStats stats) => store.Query(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats).ToList())
                .CodeTime.ShouldBeLessThan(200);
        }

        [Fact]
        public void SimpleQueryWithMaterializationToProjection()
        {
            Time((out QueryStats stats) => store.Query<LocalEntity>(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats).ToList())
                .CodeTime.ShouldBeLessThan(20);
        }

        [Fact]
        public void QueryWithWindow()
        {
            Time((out QueryStats stats) => store.Query(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats, skip: 200, take: 500))
                .DbTime.ShouldBeLessThan(250);
        }

        [Fact]
        public void QueryWithLateWindow()
        {
            Time((out QueryStats stats) => store.Query(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats, skip: 9500, take: 500))
                .DbTime.ShouldBeLessThan(350);
        }

        [Fact]
        public void QueryWithWindowMaterializedToProjection()
        {
            Time((out QueryStats stats) => store.Query<LocalEntity>(store.Configuration.GetDesignFor<LocalEntity>().Table, out stats, skip: 200, take: 500).ToList())
                .TotalTime.ShouldBeLessThan(200);
        }

        public void SetFixture(Fixture data)
        {
            store = data.Store;
            Console.WriteLine(data.I);
        }

        public class Fixture : HybridDbTests
        {
            public IDocumentStore Store => store;

            public long I { get; private set; }

            public Fixture()
            {
                store = (DocumentStore)Using(
                    DocumentStore.ForTesting(
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
                        new { SomeNumber = i, SomeData = "ABC" }));
                }

                var startNew = Stopwatch.StartNew();
                store.Execute(commands.ToArray());
                startNew.Stop();
                I = startNew.ElapsedMilliseconds;
            }
        }
    }
}