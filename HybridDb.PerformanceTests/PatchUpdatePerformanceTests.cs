using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HybridDb.Commands;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.PerformanceTests
{
    public class PatchUpdatePerformanceTests
    {
        readonly DocumentStore store;

        public PatchUpdatePerformanceTests()
        {
            const string connectionString = "data source=.;Integrated Security=True";
            store = DocumentStore.ForTestingWithTempTables(connectionString);
            store.DocumentsFor<Entity>().WithProjection(x => x.SomeNumber);
            store.Migration.InitializeDatabase();
        }

        [Fact]
        public void InsertPerformance()
        {
            var bytes1 = File.ReadAllBytes("large.json");
            var bytes2 = File.ReadAllBytes("large.json");

            // warm up
            var id = Guid.NewGuid();
            var etag = store.Execute(new InsertCommand(store.Configuration.GetTableFor<Entity>(), id, bytes1, new { SomeNumber = -1 }));

            var watch = Stopwatch.StartNew();
            var random = new Random();

            var n1 = new byte[1000];
            var n2 = new byte[1000];
            Array.Copy(bytes1, 0, n1, 0, 500);
            Array.Copy(bytes1, 0, n2, 0, 500);
            for (int i = 0; i < 1; i++)
            {
                var offset = random.Next(0, bytes2.Length);
                n2[115] = (byte) (65);
                etag = store.Execute(new PatchUpdateCommand(store.Configuration.GetTableFor<Entity>(), id, etag, n1, n2, new { SomeNumber = i }, false));
            }

            watch.ElapsedMilliseconds.ShouldBeLessThan(10000);
        }
    }
}