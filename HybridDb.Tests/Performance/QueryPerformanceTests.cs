using System;
using System.Collections.Generic;
using System.Diagnostics;
using Shouldly;
using Xunit;
using System.Linq;

namespace HybridDb.Tests.Performance
{
    public class QueryPerformanceTests : IUseFixture<QueryPerformanceFixture>
    {
        DocumentStore store;

        [Fact]
        public void test()
        {
            QueryStats rows;
            store.Query(store.Configuration.GetDesignFor<Entity>().Table, out rows).ToList();
            var watch = Stopwatch.StartNew();
            store.Query(store.Configuration.GetDesignFor<Entity>().Table, out rows).ToList();
            Console.WriteLine(watch.ElapsedMilliseconds);
            watch.Restart();
            store.Query<Entity>(store.Configuration.GetDesignFor<Entity>().Table, out rows).ToList();
            Console.WriteLine(watch.ElapsedMilliseconds);
        }

        [Fact]
        public void test2()
        {
            QueryStats rows;
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                store.Query(store.Configuration.GetDesignFor<Entity>().Table, out rows).ToList();
            }
            Console.WriteLine(watch.ElapsedMilliseconds);
        }

        [Fact]
        public void SimpleQuery()
        {
            var watch = Stopwatch.StartNew();
            QueryStats rows;
            store.Query(store.Configuration.GetDesignFor<Entity>().Table, out rows);
            watch.ElapsedMilliseconds.ShouldBeLessThan(60);
        }

        [Fact]
        public void QueryWithWindow()
        {
            var watch = Stopwatch.StartNew();
            QueryStats rows;
            store.Query(store.Configuration.GetDesignFor<Entity>().Table, out rows, skip: 200, take: 500);
            watch.ElapsedMilliseconds.ShouldBeLessThan(10);
        }

        [Fact]
        public void QueryWithLateWindow()
        {
            var watch = Stopwatch.StartNew();
            QueryStats rows;
            store.Query(store.Configuration.GetDesignFor<Entity>().Table, out rows, skip: 9500, take: 500);
            watch.ElapsedMilliseconds.ShouldBeLessThan(10);
        }

        [Fact]
        public void QueryWithWindowAndProjection()
        {
            var watch = Stopwatch.StartNew();
            QueryStats rows;
            store.Query<Entity>(store.Configuration.GetDesignFor<Entity>().Table, out rows, skip: 200, take: 500);
            watch.ElapsedMilliseconds.ShouldBeLessThan(10);
        }

        public void SetFixture(QueryPerformanceFixture data)
        {
            store = data.Store;
        }
    }
}