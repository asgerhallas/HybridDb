using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HybridDb.Commands;
using System.Linq;

namespace HybridDb.Tests.Performance
{
    public class PerformanceFixture : IDisposable
    {
        readonly DocumentStore store;

        public DocumentStore Store
        {
            get { return store; }
        }

        public decimal Baseline { get; private set; }

        public PerformanceFixture()
        {
            const string connectionString = "data source=.;Integrated Security=True";
            store = DocumentStore.ForTestingWithTempTables(connectionString);
            store.Document<Entity>()
                .Project(x => x.SomeData)
                .Project(x => x.SomeNumber);
            store.MigrateSchemaToMatchConfiguration();

            var commands = new List<DatabaseCommand>();
            for (int i = 0; i < 10000; i++)
            {
                commands.Add(new InsertCommand(store.Configuration.GetDesignFor<Entity>().Table,
                                               Guid.NewGuid(),
                                               new { SomeNumber = i, SomeData = "ABC" }));
            }

            store.Execute(commands.ToArray());

            // do some warmup
            QueryStats rows;
            store.Query(store.Configuration.GetDesignFor<Entity>().Table, out rows);
            store.Query(store.Configuration.GetDesignFor<Entity>().Table, out rows, skip: 10, take: 10);
            store.RawQuery<object>("select * from #Entities").ToList();

            //Establish baseline
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
                store.RawQuery<object>("select * from #Entities").ToList();
            Baseline = watch.ElapsedMilliseconds / 270m;
        }

        public void Dispose()
        {
            store.Dispose();
        }
    }
}