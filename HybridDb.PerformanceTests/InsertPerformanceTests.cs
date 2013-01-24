using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridDb.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.PerformanceTests
{
    public class InsertPerformanceTests
    {
        readonly DocumentStore store;

        public InsertPerformanceTests()
        {
            const string connectionString = "data source=.;Integrated Security=True";
            store = DocumentStore.ForTesting(connectionString);
            store.ForDocument<Entity>().Projection(x => x.SomeNumber);
            store.Initialize();
        }

        [Fact]
        public void InsertPerformance()
        {
            // warm up
            store.Execute(new InsertCommand(store.Configuration.GetTableFor<Entity>(), Guid.NewGuid(), new byte[0], new { SomeNumber = -1 }));

            var watch = Stopwatch.StartNew();
            var commands = new List<DatabaseCommand>();
            for (int i = 0; i < 10000; i++)
            {
                commands.Add(new InsertCommand(store.Configuration.GetTableFor<Entity>(), Guid.NewGuid(), new byte[0], new { SomeNumber = i }));
            }

            store.Execute(commands.ToArray());

            watch.ElapsedMilliseconds.ShouldBeLessThan(10000);
        }
    }
}