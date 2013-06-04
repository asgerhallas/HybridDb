using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridDb.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Performance
{
    public class InsertPerformanceTests
    {
        readonly DocumentStore store;

        public InsertPerformanceTests()
        {
            const string connectionString = "data source=.;Integrated Security=True";
            store = DocumentStore.ForTestingWithTempTables(connectionString);
            store.Document<Entity>().Project(x => x.SomeNumber);
            store.MigrateSchemaToMatchConfiguration();
        }

        [Fact]
        public void InsertPerformance()
        {
            // warm up
            store.Execute(new InsertCommand(store.Configuration.GetDesignFor<Entity>().Table, Guid.NewGuid(), new { SomeNumber = -1 }));

            var watch = Stopwatch.StartNew();
            var commands = new List<DatabaseCommand>();
            for (int i = 0; i < 10000; i++)
            {
                commands.Add(new InsertCommand(store.Configuration.GetDesignFor<Entity>().Table, Guid.NewGuid(), new { SomeNumber = i }));
            }

            store.Execute(commands.ToArray());

            watch.ElapsedMilliseconds.ShouldBeLessThan(10000);
        }
    }
}