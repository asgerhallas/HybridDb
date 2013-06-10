using System;
using System.Diagnostics;
using System.Threading;
using Shouldly;
using Xunit;
using System.Linq;

namespace HybridDb.Tests.Performance
{
    public class QueryPerformanceTests : IUseFixture<PerformanceFixture>
    {
        readonly Stopwatch watch;
        
        DocumentStore store;
        decimal baseline;
        QueryStats stats;

        public void SetFixture(PerformanceFixture data)
        {
            store = data.Store;
            baseline = data.Baseline;
            Console.WriteLine("Baseline is " + baseline);
        }

        public QueryPerformanceTests()
        {
            watch = new Stopwatch();

            //prevent the JIT Compiler from optimizing Fkt calls away
            long seed = Environment.TickCount;

            //use the second Core/Processor for the test
            Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(2);

            //prevent "Normal" Processes from interrupting Threads
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            //prevent "Normal" Threads from interrupting this thread
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }

        [Fact]
        public void SimpleQueryWithoutMaterialization()
        {
            AssertTiming(() => store.Query(store.Configuration.GetDesignFor<Entity>().Table, out stats), 40);
        }

        [Fact]
        public void SimpleQueryWithMaterializationToDictionary()
        {
            AssertTiming(() => store.Query(store.Configuration.GetDesignFor<Entity>().Table, out stats).ToList(), 40, true);
        }

        [Fact]
        public void SimpleQueryWithMaterializationToProjection()
        {
            AssertTiming(() => store.Query<Entity>(store.Configuration.GetDesignFor<Entity>().Table, out stats).ToList(), 22, true);
        }

        [Fact]
        public void QueryWithWindow()
        {
            AssertTiming(() => store.Query(store.Configuration.GetDesignFor<Entity>().Table, out stats, skip: 200, take: 500), 7);
        }

        [Fact]
        public void QueryWithLateWindow()
        {
            AssertTiming(() => store.Query(store.Configuration.GetDesignFor<Entity>().Table, out stats, skip: 9500, take: 500), 8);
        }

        [Fact]
        public void QueryWithWindowMaterializedToProjection()
        {
            AssertTiming(() => store.Query<Entity>(store.Configuration.GetDesignFor<Entity>().Table, out stats, skip: 200, take: 500).ToList(), 25);
        }

        //http://stackoverflow.com/a/16157458/64105
        void AssertTiming(Action action, int ms, bool minusQueryTime = false)
        {
            // warmup
            action();

            const int iterations = 100;

            long highest = long.MinValue;
            long lowest = long.MaxValue;
            for (int i = 0; i < iterations; i++)
            {
                watch.Restart();
                action();

                var actual = watch.ElapsedMilliseconds;
                if (minusQueryTime)
                    actual -= stats.QueryDurationInMilliseconds;

                highest = Math.Max(highest, actual);
                lowest = Math.Min(lowest, actual);
            }

            Console.WriteLine("Assert that runtime " + lowest + " is less than expected " + ms * baseline);

            lowest.ShouldBeLessThan(ms * baseline);
        }
    }
}