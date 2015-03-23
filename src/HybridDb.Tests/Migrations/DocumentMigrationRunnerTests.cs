using System;
using System.Threading;
using HybridDb.Config;
using HybridDb.Migrations;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations
{
    public class DocumentMigrationRunnerTests  : HybridDbStoreTests
    {
        [Theory]
        [InlineData(true, 42)]
        [InlineData(false, 0)]
        public void ReprojectsWhenAwaitingReprojection(bool awaitsReprojection, int result)
        {
            Document<Entity>().With(x => x.Number);

            var id = Guid.NewGuid();
            var table = new DocumentTable("Entities");
            store.Insert(table, id, new
            {
                AwaitsReprojection = awaitsReprojection, 
                Discriminator = "Entity",
                Version = 0,
                Document = configuration.Serializer.Serialize(new Entity { Number = 42 })
            });

            new DocumentMigrationRunner(logger, store).RunSynchronously();

            var row = store.Get(table, id);
            row["Number"].ShouldBe(result);
            row["AwaitsReprojection"].ShouldBe(false);
        }

        [Fact]
        public void QueriesInSetsAndUpdatesOneByOne()
        {
            Document<Entity>().With(x => x.Number);

            for (int i = 0; i < 200; i++)
            {
                store.Insert(new DocumentTable("Entities"), Guid.NewGuid(), new
                {
                    Discriminator = "Entity", 
                    Version = 0, 
                    Document = configuration.Serializer.Serialize(new Entity())
                });
            }

            // bump the version of the configuration
            UseMigrations(new InlineMigration(1));

            var initialNumberOfRequests = store.NumberOfRequests;
            new DocumentMigrationRunner(logger, store).RunSynchronously();

            // 1: query for documents below version, return 100
            // 101: update documents one at a time
            // 102: query for documents below version, return 100
            // 202: update documents one at a time
            // 203: query for documents below version, returns 0
            (store.NumberOfRequests - initialNumberOfRequests).ShouldBe(203);
        }

        [Fact]
        public void AcceptsConcurrentWrites()
        {
            UseRealTables();

            Document<Entity>().With(x => x.Number);

            var id = Guid.NewGuid();
            var table = new DocumentTable("Entities");
            var etag = store.Insert(table, id, new
            {
                Discriminator = "Entity",
                Version = 0,
                Document = configuration.Serializer.Serialize(new Entity())
            });

            var gate1 = new ManualResetEvent(false);
            var gate2 = new ManualResetEvent(false);

            UseMigrations(new InlineMigration(1, new ChangeDocument<Entity>((serializer, bytes) =>
            {
                gate1.Set();
                Thread.Sleep(1000);
                return bytes;
            })));

            bool? failed = null;

            new DocumentMigrationRunner(logger, store)
                .RunInBackground()
                .ContinueWith(x =>
                {
                    failed = x.IsFaulted;
                    gate2.Set();
                });

            gate1.WaitOne();

            store.Update(table, id, etag, new {});

            gate2.WaitOne();

            failed.ShouldBe(false);
        }
    }
}