using System;
using System.Linq;
using System.Text;
using System.Threading;
using FakeItEasy;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Migrations.Documents;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

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

            new DocumentMigrationRunner().Run(store).Wait();

            var row = store.Get(table, id);
            row["Number"].ShouldBe(result);
            row["AwaitsReprojection"].ShouldBe(false);
            row[DocumentTable.VersionColumn].ShouldBe(0);
        }

        [Fact]
        public void DoesNotRetrieveDocumentIfNoReprojectionOrMigrationIsNeededButUpdatesVersion()
        {
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

            Document<Entity>().With(x => x.Number);
            Document<OtherEntity>().With(x => x.Number);

            // add migration for same version and one for other document
            UseMigrations(
                new InlineMigration(1, new ChangeDocument<Entity>((serializer, bytes) => bytes)),
                new InlineMigration(2, new ChangeDocument<OtherEntity>((serializer, bytes) => bytes)));

            InitializeStore();

            // 3 UpdateProjections (DocumentTables)
            // + 2 for inline migrations
            store.Stats.NumberOfQueries.ShouldBe(5);
            store.Stats.NumberOfGets.ShouldBe(0);
            store.Stats.NumberOfCommands.ShouldBe(0);

            var row = store.Get(table, id);
            row[DocumentTable.VersionColumn].ShouldBe(1);
        }

        [Fact(Skip = "sideshow")]
        public void QueriesInSetsAndUpdatesOneByOne()
        {
            var fakeStore = A.Fake<IDocumentStore>(x => x.Wrapping(store));

            Document<Entity>().With(x => x.Number);

            var documentTable = new DocumentTable("Entities");

            for (int i = 0; i < 200; i++)
            {
                store.Insert(documentTable, NewId(), new
                {
                    Discriminator = typeof(Entity).AssemblyQualifiedName, 
                    Version = 0, 
                    Document = configuration.Serializer.Serialize(new Entity())
                });
            }

            // bump the version of the configuration
            UseMigrations(new InlineMigration(1));

            new DocumentMigrationRunner().Run(store).Wait();

            // 1+2: Entities table => 100 rows
            // 3: Entities table => 0 rows
            A.CallTo(fakeStore)
                .Where(x => x.Method.Name == "Query")
                .WhenArgumentsMatch(x => x.Get<DocumentTable>(0).Name == "Entities")
                .MustHaveHappened(3, Times.Exactly);

            // each document is being updated individually
            A.CallTo(fakeStore)
                .Where(x => x.Method.Name == "Execute")
                .WhenArgumentsMatch(x => x.Get<DmlCommand[]>(0)[0] is UpdateCommand)
                .MustHaveHappened(200, Times.Exactly);
        }

        [Fact]
        public void AcceptsConcurrentWrites()
        {
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

            new DocumentMigrationRunner()
                .Run(store)
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

            new DocumentMigrationRunner().Run(store).Wait();

            store.Stats.NumberOfRequests.ShouldBe(0);
        }

        [Fact]
        public void DoesNotStartBackgroundProcessWhenAllMigrationsAreTurnedOff()
        {
            DisableMigrations();
            Document<Entity>().With(x => x.Number);

            new DocumentMigrationRunner().Run(store).Wait();

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

            InitializeStore();

            UseMigrations(new InlineMigration(1, new ChangeDocument<Entity>((x, y) => throw new Exception())));

            Should.NotThrow(() =>
            {
                new DocumentMigrationRunner().Run(store).Wait(1000);
            });

            var numberOfErrors = log.Count(x =>
                x.RenderMessage() == $"Unrecoverable exception while migrating document of type '\"HybridDb.Tests.HybridDbTests+Entity\"' with id '\"{id}\"'. Stopping migrator for table '\"Entities\"'.");

            // it does not retry exception
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

            InitializeStore();

            backupWriter.Files.Count.ShouldBe(1);
            backupWriter.Files[$"HybridDb.Tests.HybridDbTests+Entity_{id}_0.bak"]
                .ShouldBe(Encoding.UTF8.GetBytes(configuration.Serializer.Serialize(new Entity { Id = id, Property = "Asger" })));
        }
    }
}