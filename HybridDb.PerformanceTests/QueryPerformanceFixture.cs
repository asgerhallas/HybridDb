using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridDb.Commands;

namespace HybridDb.PerformanceTests
{
    public class QueryPerformanceFixture : IDisposable
    {
        readonly DocumentStore store;

        public DocumentStore Store
        {
            get { return store; }
        }

        public QueryPerformanceFixture()
        {
            const string connectionString = "data source=.;Integrated Security=True";
            store = DocumentStore.ForTesting(connectionString);
            store.DocumentsFor<Entity>()
                .WithProjection(x => x.SomeData)
                .WithProjection(x => x.SomeNumber);
            store.Migration.InitializeDatabase();

            var commands = new List<DatabaseCommand>();
            for (int i = 0; i < 10000; i++)
            {
                commands.Add(new InsertCommand(store.Configuration.GetTableFor<Entity>(), Guid.NewGuid(), new byte[0], new { SomeNumber = i }));
            }

            store.Execute(commands.ToArray());

            // Warm up with a query
            var watch = Stopwatch.StartNew();

            QueryStats rows;
            store.Query(store.Configuration.GetTableFor<Entity>(), out rows);

            if (watch.ElapsedMilliseconds > 200)
                Console.WriteLine("Warm up takes longer than expected");
        }

        public void Dispose()
        {
            store.Dispose();
        }
    }
}