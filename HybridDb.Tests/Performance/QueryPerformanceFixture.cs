using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridDb.Commands;

namespace HybridDb.Tests.Performance
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
            store = DocumentStore.ForTestingWithTempTables(connectionString);
            store.DocumentsFor<Entity>()
                .Project(x => x.SomeData)
                .Project(x => x.SomeNumber);
            store.InitializeDatabase();

            var commands = new List<DatabaseCommand>();
            for (int i = 0; i < 10000; i++)
            {
                commands.Add(new InsertCommand(store.Configuration.GetSchemaFor<Entity>().Table, Guid.NewGuid(), new byte[0], new { SomeNumber = i }));
            }

            store.Execute(commands.ToArray());

            // Warm up with a query
            var watch = Stopwatch.StartNew();

            QueryStats rows;
            store.Query(store.Configuration.GetSchemaFor<Entity>().Table, out rows);

            if (watch.ElapsedMilliseconds > 200)
                Console.WriteLine("Warm up takes longer than expected");
        }

        public void Dispose()
        {
            store.Dispose();
        }
    }
}