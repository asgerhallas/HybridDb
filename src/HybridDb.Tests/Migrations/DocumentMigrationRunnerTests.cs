using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Linq.Bonsai;
using HybridDb.Migrations;
using HybridDb.Migrations.Documents;
using Newtonsoft.Json;
using Serilog.Events;
using ShouldBeLike;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static HybridDb.Helpers;

namespace HybridDb.Tests.Migrations
{
    public class DocumentMigrationRunnerTests : HybridDbTests
    {
        public DocumentMigrationRunnerTests(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData(true, 42)]
        [InlineData(false, 0)]
        public void ReprojectsWhenAwaitingReprojection(bool awaitsReprojection, int result)
        {
            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);

            var id = NewId();
            var table = new DocumentTable("Entities");
            store.Insert(table, id, new
            {
                AwaitsReprojection = awaitsReprojection, 
                Discriminator = typeof(Entity).AssemblyQualifiedName,
                Version = 0,
                Document = configuration.Serializer.Serialize(new Entity { Number = 42 })
            });

            new DocumentMigrationRunner(store).Run().Wait();

            var row = store.Get(table, id);
            row["Number"].ShouldBe(result);
            row["AwaitsReprojection"].ShouldBe(false);
            row[DocumentTable.VersionColumn].ShouldBe(0);
        }

        [Fact]
        public async Task DoesNotRetrieveDocumentIfNoReprojectionOrMigrationIsNeededButUpdatesVersion()
        {
            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);
            Document<OtherEntity>().With(x => x.Number);

            var id = NewId();
            var table = configuration.GetDesignFor<Entity>().Table;

            store.Insert(table, id, new
            {
                AwaitsReprojection = false,
                Discriminator = typeof(Entity).AssemblyQualifiedName,
                Version = 1,
                Document = configuration.Serializer.Serialize(new Entity())
            });

            ResetConfiguration();

            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);
            Document<OtherEntity>().With(x => x.Number);

            // add migration for same version and one for other document
            UseMigrations(
                new InlineMigration(1, new ChangeDocument<Entity>((serializer, bytes) => bytes)),
                new InlineMigration(2, new ChangeDocument<OtherEntity>((serializer, bytes) => bytes)));

            TouchStore();

            await store.DocumentMigration;

            // 3 UpdateProjections (DocumentTables)
            // + 2 for inline migrations
            store.Stats.NumberOfQueries.ShouldBe(5);
            store.Stats.NumberOfGets.ShouldBe(0);
            store.Stats.NumberOfCommands.ShouldBe(0);

            var row = store.Get(table, id);
            row[DocumentTable.VersionColumn].ShouldBe(1);
        }

        [Fact(Skip = "Only for getting a ballpark estimate of migration performance")]
        public void ConcurrentMigrationPerformance()
        {
            Document<Entity>().With(x => x.Number);

            using (var session = store.OpenSession())
            {
                for (int i = 0; i < 100000; i++)
                {
                    session.Store(new Entity()
                    {
                        Id = NewId(),
                    });
                }

                session.SaveChanges();
            }

            var migration1 = new TrackingCommand();
            UseMigrations(new InlineMigration(1, migration1));

            var sw = Stopwatch.StartNew();
            
            Task.WaitAll(
                new DocumentMigrationRunner(store).Run(),
                new DocumentMigrationRunner(store).Run());

            // Only for a ballpark estimate
            // Takes about 26000ms on my machine
            output.WriteLine(sw.ElapsedMilliseconds.ToString());
        }

        [Fact]
        public void AcceptsConcurrentWrites()
        {
            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            UseRealTables();

            Document<Entity>().With(x => x.Number);

            var id = NewId();
            var table = new DocumentTable("Entities");
            var etag = store.Insert(table, id, new
            {
                Discriminator = typeof(Entity).AssemblyQualifiedName,
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

            new DocumentMigrationRunner(store)
                .Run()
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
        public void DoesNotStartBackgroundProcessWhenTurnedOff()
        {
            DisableBackgroundMigrations();
            Document<Entity>().With(x => x.Number);

            new DocumentMigrationRunner(store).Run().Wait();

            store.Stats.NumberOfRequests.ShouldBe(0);
        }

        [Fact]
        public void StopsIfMigrationFails()
        {
            Document<Entity>().With(x => x.Number);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                session.SaveChanges();
            }

            ResetConfiguration();
            Document<Entity>().With(x => x.Number);

            TouchStore();

            UseMigrations(new InlineMigration(1, new ChangeDocument<Entity>((x, y) => throw new Exception())));

            Should.NotThrow(() =>
            {
                new DocumentMigrationRunner(store).Run().Wait(1000);
            });

            var numberOfErrors = log.Count(x =>
                x.RenderMessage() == $"Unrecoverable exception while migrating document of type '\"HybridDb.Tests.HybridDbTests+Entity\"' with id '\"{id}\"'. Stopping migrator for table '\"Entities\"'.");

            // it does not retry exception
            numberOfErrors.ShouldBe(1);
        }

        [Fact]
        public void StopsIfMigrationFails_BeforeLoadingDocument()
        {
            Document<Entity>().With(x => x.Number);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                session.SaveChanges();
            }

            ResetConfiguration();
            Document<Entity>().With(x => x.Number);

            TouchStore();

            UseMigrations(new InlineMigration(1, new MigrationFailsBeforeLoadingDocument(typeof(Entity))));

            Should.NotThrow(() => new DocumentMigrationRunner(store).Run().Wait(1000));

            var numberOfErrors = log.Count(x => 
                x.RenderMessage() == "DocumentMigrationRunner failed and stopped. Documents will not be migrated in background until you restart the runner. They will still be migrated on Session.Load() and Session.Query().");

            numberOfErrors.ShouldBe(1);
        }

        [Fact]
        public void BacksUpMigratedDocumentBeforeMigration()
        {
            var id = NewId();
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges();
            }

            ResetConfiguration();
            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "1"; })));

            var backupWriter = new FakeBackupWriter();
            UseBackupWriter(backupWriter);

            TouchStore();

            backupWriter.Files.Count.ShouldBe(1);
            backupWriter.Files[$"HybridDb.Tests.HybridDbTests+Entity_{id}_0.bak"]
                .ShouldBe(Encoding.UTF8.GetBytes(configuration.Serializer.Serialize(new Entity { Id = id, Property = "Asger" })));
        }

        [Fact]
        public async Task MigratesSelectedIds()
        {
            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);

            var table = configuration.GetDesignFor<Entity>().Table;

            void Insert(string id) =>
                store.Insert(table, id, new
                {
                    AwaitsReprojection = false,
                    Discriminator = typeof(Entity).AssemblyQualifiedName,
                    Version = 0,
                    Document = configuration.Serializer.Serialize(new Entity())
                });

            Insert("a");
            Insert("B");
            Insert("C");
            Insert("d");
            Insert("E");
            Insert("f");
            Insert("g");
            Insert("H");

            ResetConfiguration();

            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);

            var migratedIds = new List<string>();

            UseMigrations(
                new InlineMigration(1, new ChangeDocument<Entity>(ListOf(new IdMatcher(new []{ "b", "d", "e", "G" })),
                    (session, serializer, r) =>
                    {
                        migratedIds.Add(r.Get(DocumentTable.IdColumn));
                        return r.Get(DocumentTable.DocumentColumn);
                    })));

            TouchStore();

            await store.DocumentMigration;
            
            log.Where(x => x.MessageTemplate.Text == "Migrating {NumberOfDocumentsInBatch} documents from {Table}. {NumberOfPendingDocuments} documents left.")
                .Sum(x => (int)((ScalarValue)x.Properties["NumberOfPendingDocuments"]).Value)
                .ShouldBe(4);

            migratedIds.ShouldBeLikeUnordered("B", "d", "E", "g");
        }

        [Fact]
        public async Task MigratesByPrefix()
        {
            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);

            var table = configuration.GetDesignFor<Entity>().Table;

            void Insert(string id) =>
                store.Insert(table, id, new
                {
                    AwaitsReprojection = false,
                    Discriminator = typeof(Entity).AssemblyQualifiedName,
                    Version = 0,
                    Document = configuration.Serializer.Serialize(new Entity())
                });

            Insert("atest");
            Insert("Ahest");
            Insert("aatest");
            Insert("AaAtest");
            Insert("EAatest");
            Insert("ftest");
            Insert("gatest");
            Insert("Htest");

            ResetConfiguration();

            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);

            var migratedIds = new List<string>();

            UseMigrations(
                new InlineMigration(1, new ChangeDocument<Entity>(ListOf(new IdPrefixMatcher("aA")),
                    (session, serializer, r) =>
                    {
                        migratedIds.Add(r.Get(DocumentTable.IdColumn));
                        return r.Get(DocumentTable.DocumentColumn);
                    })));

            TouchStore();

            await store.DocumentMigration;

            log.Where(x => x.MessageTemplate.Text == "Migrating {NumberOfDocumentsInBatch} documents from {Table}. {NumberOfPendingDocuments} documents left.")
                .Sum(x => (int)((ScalarValue)x.Properties["NumberOfPendingDocuments"]).Value)
                .ShouldBe(2);

            migratedIds.ShouldBeLikeUnordered("aatest", "AaAtest");
        }

        [Fact]
        public async Task MigratesSelectedIdsAndIdPrefixCombined()
        {
            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);

            var table = configuration.GetDesignFor<Entity>().Table;

            void Insert(string id) =>
                store.Insert(table, id, new
                {
                    AwaitsReprojection = false,
                    Discriminator = typeof(Entity).AssemblyQualifiedName,
                    Version = 0,
                    Document = configuration.Serializer.Serialize(new Entity())
                });

            Insert("a");
            Insert("B");
            Insert("C");
            Insert("d");
            Insert("E");
            Insert("f");
            Insert("g");
            Insert("H");
            Insert("atest");
            Insert("Ahest");
            Insert("aatest");
            Insert("AaAtest");
            Insert("EAatest");
            Insert("ftest");
            Insert("gatest");
            Insert("Htest");

            ResetConfiguration();

            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);

            var migratedIds = new List<string>();

            UseMigrations(
                new InlineMigration(1, new ChangeDocument<Entity>(ListOf<IDocumentMigrationMatcher>(
                        new IdPrefixMatcher("a"),
                        new IdMatcher(new[] { "b", "aatest", "e", "G" })),
                    (session, serializer, r) =>
                    {
                        migratedIds.Add(r.Get(DocumentTable.IdColumn));
                        return r.Get(DocumentTable.DocumentColumn);
                    })));

            TouchStore();

            await store.DocumentMigration;

            log.Where(x => x.MessageTemplate.Text == "Migrating {NumberOfDocumentsInBatch} documents from {Table}. {NumberOfPendingDocuments} documents left.")
                .Sum(x => (int)((ScalarValue)x.Properties["NumberOfPendingDocuments"]).Value)
                .ShouldBe(1);

            migratedIds.ShouldBeLikeUnordered("aatest");
        }

        [Fact]
        public async Task DeleteDocument()
        {
            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);

            var table = configuration.GetDesignFor<Entity>().Table;

            void Insert(string id, int number) =>
                store.Insert(table, id, new
                {
                    AwaitsReprojection = false,
                    Discriminator = typeof(Entity).AssemblyQualifiedName,
                    Version = 0,
                    Document = configuration.Serializer.Serialize(new Entity { Id = id }),
                    Number = number
                });

            Insert("atest", 1);
            Insert("aatest", 2);
            Insert("AaAtest", 3);

            ResetConfiguration();

            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);

            UseMigrations(
                new InlineMigration(1, new DeleteDocuments<Entity>(new IdMatcher(new[] { "aatest", "AaAtest" }))));

            await store.DocumentMigration;

            log.Where(x => x.MessageTemplate.Text == "Migrating {NumberOfDocumentsInBatch} documents from {Table}. {NumberOfPendingDocuments} documents left.")
                .Sum(x => (int)((ScalarValue)x.Properties["NumberOfPendingDocuments"]).Value)
                .ShouldBe(2);

            var entities = store.OpenSession().Query<Entity>().ToList();
            entities.Count.ShouldBe(1);
            entities.Single().Id.ShouldBe("atest");
        }

        [Fact]
        public async Task DeleteDocumentsFromTypedTable()
        {
            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);
            Document<OtherEntity>().With(x => x.Number);

            void Insert<T>(string id, int number) =>
                store.Insert(configuration.GetDesignFor<T>().Table, id, new
                {
                    AwaitsReprojection = false,
                    Discriminator = typeof(T).AssemblyQualifiedName,
                    Version = 0,
                    Document = configuration.Serializer.Serialize(new { Id = id }),
                    Number = number
                });

            Insert<Entity>("atest", 1);
            Insert<Entity>("aatest", 2);
            Insert<Entity>("AaAtest", 3);
            Insert<OtherEntity>("atest", 1);
            Insert<OtherEntity>("aatest", 2);
            Insert<OtherEntity>("AaAtest", 3);

            ResetConfiguration();

            UseTypeMapper(new AssemblyQualifiedNameTypeMapper());
            Document<Entity>().With(x => x.Number);
            Document<OtherEntity>().With(x => x.Number);

            UseMigrations(
                new InlineMigration(1, new DeleteDocuments<Entity>(new IdMatcher(new[] { "aatest", "AaAtest" }))));

            await store.DocumentMigration;

            var session = store.OpenSession();
            var entities = session.Query<Entity>().ToList();
            entities.Count.ShouldBe(1);
            entities.Single().Id.ShouldBe("atest");

            var otherEntities = session.Query<OtherEntity>().ToList();
            otherEntities.Count.ShouldBe(3);
        }

        public class TrackingCommand : DocumentRowMigrationCommand
        {
            public List<string> MigratedIds { get; private set; } = new();

            public TrackingCommand() : base(null, null) { }

            public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row)
            {
                MigratedIds.Add(row.Get(DocumentTable.IdColumn));
                return row;
            }
        }

        public class MigrationFailsBeforeLoadingDocument : DocumentRowMigrationCommand
        {
            public MigrationFailsBeforeLoadingDocument(Type type, params IDocumentMigrationMatcher[] matchers) : base(type, matchers)
            {
            }

            public override SqlBuilder Matches(IDocumentStore store, int? version) => throw new Exception("Hej do");

            public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row) => throw new NotImplementedException();
        }
    }
}