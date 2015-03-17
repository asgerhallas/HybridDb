using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
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

            new DocumentMigrationRunner(store, configuration).RunSynchronously();

            var row = store.Get(table, id);
            row["Number"].ShouldBe(result);
        }

        [Fact]
        public void MigratesSetOfDocuments()
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
            new DocumentMigrationRunner(store, configuration).RunSynchronously();

            // 1: query for documents below version, return 100
            // 2: update documents in batch
            // 3: query for documents below version, return 100
            // 4: update documents in batch
            // 5: query for documents below version, returns 0
            (store.NumberOfRequests - initialNumberOfRequests).ShouldBe(5);
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

            new DocumentMigrationRunner(store, configuration)
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

        [Fact]
        public void RunsProjectionsForDocumentsWithNewColumns()
        {
            Document<Entity>();
            Document<AbstractEntity>();
            Document<DerivedEntity>();
            Document<OtherEntity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Number = 1 });
                session.Store(new Entity { Number = 2 });
                session.Store(new DerivedEntity { Property = "Asger" });
                session.Store(new DerivedEntity { Property = "Peter" });
                session.Store(new OtherEntity { Number = 41 });
                session.Store(new OtherEntity { Number = 42 });
                session.SaveChanges();
            }

            Reset();

            // change configuration
            Document<Entity>().With(x => x.Number);
            Document<AbstractEntity>().With(x => x.Property);
            Document<DerivedEntity>();
            Document<OtherEntity>();

            // this will trigger migration
            InitializeStore();

            using (var managedConnection = database.Connect())
            {
                var result1 = managedConnection.Connection.Query<bool, int, Tuple<bool, int>>(
                    "select AwaitsReprojection, Number from #Entities order by Number", Tuple.Create, splitOn: "*").ToList();

                result1[0].ShouldBe(Tuple.Create(false, 1));
                result1[1].ShouldBe(Tuple.Create(false, 2));

                var result2 = managedConnection.Connection.Query<bool, string, Tuple<bool, string>>(
                    "select AwaitsReprojection, Property from #AbstractEntities order by Property", Tuple.Create, splitOn: "*").ToList();

                result2[0].ShouldBe(Tuple.Create(false, "Asger"));
                result2[1].ShouldBe(Tuple.Create(false, "Peter"));

                var result3 = managedConnection.Connection.Query<bool>(
                    "select AwaitsReprojection from #OtherEntities").ToList();

                result3[0].ShouldBe(false);
                result3[1].ShouldBe(false);
            }
        }

    }
}