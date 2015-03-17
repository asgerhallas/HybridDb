using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using HybridDb.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Performance
{
    public class PerformanceTests : IUseFixture<PerformanceTests.SystemModifierFixture>
    {
        protected decimal systemModifier;

        public PerformanceTests()
        {
            //prevent the JIT Compiler from optimizing Fkt calls away
            long seed = Environment.TickCount;

            //use the second Core/Processor for the test
            Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(2);

            //prevent "Normal" Processes from interrupting Threads
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            //prevent "Normal" Threads from interrupting this thread
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }

        public delegate void HybridAction(out QueryStats stats);

        protected Timings Time(HybridAction action, int iterations = 100)
        {
            return Time(action, systemModifier, iterations);
        }

        protected static Timings Time(HybridAction action, decimal modifier, int iterations = 100)
        {
            var watch = new Stopwatch();

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

            timings.DbTimeLowest = (long)(timings.DbTimeLowest / modifier);
            timings.CodeTimeLowest = (long)(timings.CodeTimeLowest / modifier);

            Console.WriteLine("Lowest db time was " + timings.DbTimeLowest);
            Console.WriteLine("Lowest code time was " + timings.CodeTimeLowest);

            return timings;
        }

        protected decimal Time(Action action, int iterations = 100)
        {
            return Time(action, systemModifier, iterations);
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
                using (var store = DocumentStore.ForTesting(
                    TableMode.UseTempTables,
                    configurator: new LambdaHybridDbConfigurator(c =>
                        c.Document<Entity>()
                            .With(x => x.SomeData)
                            .With(x => x.SomeNumber))))
                {
                    var commands = new List<DatabaseCommand>();
                    for (var i = 0; i < 10; i++)
                    {
                        commands.Add(new InsertCommand(
                            store.Configuration.GetDesignFor<Entity>().Table,
                            Guid.NewGuid(),
                            new {SomeNumber = i, SomeData = "ABC"}));
                    }

                    store.Execute(commands.ToArray());

                    decimal time = 0;
                    for (var i = 0; i < 10; i++)
                    {
                        time += Time(() => ((DocumentStore)store).Database.RawQuery<object>("select * from #Entities").ToList(), 1m);
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