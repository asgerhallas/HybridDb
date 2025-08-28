using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using HybridDb.Commands;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Performance
{
    public class PerformanceTests : IClassFixture<PerformanceTests.SystemModifierFixture>
    {
        protected decimal systemModifier;

        public PerformanceTests()
        {
            //prevent the JIT Compiler from optimizing Fkt calls away
            long seed = Environment.TickCount;

            //use the second Core/Processor for the test
            //Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(2);

            //prevent "Normal" Processes from interrupting Threads
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            //prevent "Normal" Threads from interrupting this thread
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }

        public delegate void HybridAction(out QueryStats stats);

        protected Timings Time(HybridAction action, int iterations = 100) => Time(action, systemModifier, iterations);

        protected static Timings Time(HybridAction action, decimal modifier, int iterations = 100)
        {
            var watch = new Stopwatch();

            // warmup
            action(out var stats);

            var timings = new Timings
            {
                DbTime = 0,
                CodeTime = 0
            };

            for (var i = 0; i < iterations; i++)
            {
                watch.Restart();
                action(out stats);
                timings.DbTime += stats.QueryDurationInMilliseconds;

                timings.CodeTime += watch.ElapsedMilliseconds - stats.QueryDurationInMilliseconds;
            }

            timings.DbTime = (long)(timings.DbTime / modifier);
            timings.CodeTime = (long)(timings.CodeTime / modifier);

            Console.WriteLine($"Total db time over {iterations} iterations was {timings.DbTime}.");
            Console.WriteLine($"Total code time over {iterations} iterations was {timings.CodeTime}.");

            return timings;
        }

        protected static decimal Time(Action action, decimal modifier, int iterations = 100)
        {
            var watch = new Stopwatch();

            // warmup
            action();

            var ticksPerMillisecond = (decimal)Stopwatch.Frequency/1000;
            
            var timeLowest = decimal.MaxValue;
            for (var i = 0; i < iterations; i++)
            {
                watch.Restart();
                action();
                timeLowest = Math.Min(timeLowest, watch.ElapsedTicks / ticksPerMillisecond);
            }

            timeLowest = timeLowest / modifier;

            Console.WriteLine("Lowest time was " + timeLowest);

            return timeLowest;
        }

        public class Timings
        {
            public long DbTime { get; set; }
            public long CodeTime { get; set; }

            public long TotalTime => DbTime + CodeTime;
        }

        public void SetFixture(SystemModifierFixture data)
        {
            systemModifier = data.SystemModifier;
            Console.WriteLine("System modifier is " + systemModifier);
        }

        public class LocalEntity
        {
            public string Id { get; set; }
            public string SomeData { get; set; }
            public int SomeNumber { get; set; }
        }

        public class SystemModifierFixture : HybridDbTests
        {
            public SystemModifierFixture(ITestOutputHelper output) : base(output)
            {
                using (var store = DocumentStore.ForTesting(TableMode.GlobalTempTables, c =>
                {
                    c.UseConnectionString(connectionString);
                    c.Document<LocalEntity>()
                        .With(x => x.SomeData)
                        .With(x => x.SomeNumber);
                }))
                {
                    var commands = new List<HybridDbCommand>();
                    for (var i = 0; i < 10; i++)
                    {
                        commands.Add(new InsertCommand(
                            store.Configuration.GetDesignFor<LocalEntity>().Table,
                            Guid.NewGuid().ToString(),
                            new {SomeNumber = i, SomeData = "ABC"}));
                    }

                    foreach (var command in commands)
                    {
                        store.Execute(command);
                    }

                    decimal time = 0;
                    for (var i = 0; i < 10; i++)
                    {
                        time += Time(() => ((DocumentStore)store).Database.RawQuery<object>("select * from #LocalEntities").ToList(), 1m);
                    }

                    // The below constant is chosen to get as close 1.0 on my machine as possible.
                    // This must not be changed without changing the test timings accordingly.
                    // if you are writing new tests on another machine 
                    SystemModifier = time/1.6m;
                }
            }

            public decimal SystemModifier { get; private set; }
        }

    }
}