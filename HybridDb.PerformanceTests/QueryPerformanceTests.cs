using System.Diagnostics;
using Shouldly;
using Xunit;

namespace HybridDb.PerformanceTests
{
    public class QueryPerformanceTests : IUseFixture<QueryPerformanceFixture>
    {
        DocumentStore store;

        [Fact]
        public void QueryPerformance()
        {
            var watch = Stopwatch.StartNew();
            QueryStats rows;
            store.Query(store.Configuration.GetTableFor<Entity>(), out rows);
            watch.ElapsedMilliseconds.ShouldBeLessThan(60);
        }

        [Fact]
        public void QueryWithWindowPerformance()
        {
            var watch = Stopwatch.StartNew();
            QueryStats rows;
            store.Query(store.Configuration.GetTableFor<Entity>(), out rows, skip: 200, take: 500);
            watch.ElapsedMilliseconds.ShouldBeLessThan(10);
        }

        [Fact]
        public void QueryWithLateWindowPerformance()
        {
            var watch = Stopwatch.StartNew();
            QueryStats rows;
            store.Query(store.Configuration.GetTableFor<Entity>(), out rows, skip: 9500, take: 500);
            watch.ElapsedMilliseconds.ShouldBeLessThan(10);
        }

        [Fact]
        public void QueryWithWindowAndProjectionPerformance()
        {
            var watch = Stopwatch.StartNew();
            QueryStats rows;
            store.Query<Entity>(store.Configuration.GetTableFor<Entity>(), out rows, skip: 200, take: 500);
            watch.ElapsedMilliseconds.ShouldBeLessThan(10);
        }

        public void SetFixture(QueryPerformanceFixture data)
        {
            store = data.Store;
        }
    }
}