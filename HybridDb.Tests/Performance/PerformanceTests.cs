using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using HybridDb.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Performance
{
    public class PerformanceTests : IUseFixture<PerformanceTests.SystemModifierFixture>
    {
        protected readonly Stopwatch watch;
        protected decimal systemModifier;

        public PerformanceTests()
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

        //http://stackoverflow.com/a/16157458/64105
        void AssertQueryTiming(Func<QueryStats> action, int ms)
        {
            // warmup
            action();

            const int iterations = 100;

            long highest = long.MinValue;
            long lowest = long.MaxValue;
            for (int i = 0; i < iterations; i++)
            {
                var stats = action();
                var actual = stats.QueryDurationInMilliseconds;
                highest = Math.Max(highest, actual);
                lowest = Math.Min(lowest, actual);
            }

            Console.WriteLine("Assert that query " + lowest + " is less than expected " + ms * systemModifier);

            lowest.ShouldBeLessThan(ms * systemModifier);
        }

        public delegate void HybridAction(out QueryStats stats);

        protected Timings Time(HybridAction action, int iterations = 100)
        {
            QueryStats stats;

            // warmup
            action(out stats);

            var timings = new Timings
            {
                DbTimeLowest = long.MaxValue,
                CodeTimeLowest = long.MaxValue
            };

            for (var i = 0; i < iterations; i++)
            {
                watch.Restart();
                action(out stats);
                timings.DbTimeLowest = Math.Min(
                    timings.DbTimeLowest,
                    stats.QueryDurationInMilliseconds);

                timings.CodeTimeLowest = Math.Min(
                    timings.CodeTimeLowest,
                    watch.ElapsedMilliseconds - stats.QueryDurationInMilliseconds);
            }

            timings.DbTimeLowest = (long)(timings.DbTimeLowest / systemModifier);
            timings.CodeTimeLowest = (long)(timings.CodeTimeLowest / systemModifier);

            Console.WriteLine("Lowest db time was " + timings.DbTimeLowest);
            Console.WriteLine("Lowest code time was " + timings.CodeTimeLowest);

            return timings;
        }

        public class Timings
        {
            public long DbTimeLowest { get; set; }
            public long CodeTimeLowest { get; set; }
            public long TotalTimeLowest { get { return DbTimeLowest + CodeTimeLowest; } }
        }

        public void SetFixture(SystemModifierFixture data)
        {
            systemModifier = data.SystemModifier;
            Console.WriteLine("System modifier is " + systemModifier);
        }

        public class Entity
        {
            public Guid Id { get; set; }
            public string SomeData { get; set; }
            public int SomeNumber { get; set; }
        }

        public class SystemModifierFixture
        {
            public SystemModifierFixture()
            {
                using (var store = DocumentStore.ForTestingWithTempTables())
                {
                    store.Document<Entity>().Project(x => x.SomeData).Project(x => x.SomeNumber);
                    store.MigrateSchemaToMatchConfiguration();

                    var commands = new List<DatabaseCommand>();
                    for (int i = 0; i < 10; i++)
                    {
                        commands.Add(new InsertCommand(
                                         store.Configuration.GetDesignFor<Entity>().Table,
                                         Guid.NewGuid(),
                                         new { SomeNumber = i, SomeData = "ABC" }));
                    }

                    store.Execute(commands.ToArray());

                    // do some warmup
                    store.RawQuery<object>("select * from #Entities").ToList();

                    //Establish baseline
                    var watch = Stopwatch.StartNew();
                    for (int i = 0; i < 100; i++)
                        store.RawQuery<object>("select * from #Entities").ToList();
                    SystemModifier = watch.ElapsedMilliseconds / 20m;
                }
            }

            public decimal SystemModifier { get; private set; }
        }

    }
}