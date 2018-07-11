using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Migrations;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations
{
    public class DocumentMigrationRunnerTests : HybridDbTests
    {
        [Theory]
        [InlineData(true, 42)]
        [InlineData(false, 0)]
        public async Task ReprojectsWhenAwaitingReprojection(bool awaitsReprojection, int result)
        {
            Document<Entity>().With(x => x.Number);

            store.Initialize();

            var id = NewId();
            var table = new DocumentTable("Entities");
            await store.InsertAsync(table, id, new
            {
                AwaitsReprojection = awaitsReprojection,
                Discriminator = typeof(Entity).AssemblyQualifiedName,
                Version = 0,
                Document = configuration.Serializer.Serialize(new Entity { Number = 42 })
            });

            await new DocumentMigrationRunner().Run(store);

            var row = await store.GetAsync(table, id);
            row["Number"].ShouldBe(result);
            row["AwaitsReprojection"].ShouldBe(false);
            row[table.VersionColumn].ShouldBe(0);
        }

        [Fact]
        public async Task DoesNotRetrieveDocumentIfNoReprojectionOrMigrationIsNeededButUpdatesVersion()
        {
            var fakeStore = A.Fake<IDocumentStore>(x => x.Wrapping(store));

            Document<Entity>().With(x => x.Number);
            Document<OtherEntity>().With(x => x.Number);

            // add migration for same version and one for other document
            UseMigrations(
                new InlineMigration(1, new ChangeDocument<Entity>((serializer, bytes) => bytes)),
                new InlineMigration(2, new ChangeDocument<OtherEntity>((serializer, bytes) => bytes)));

            store.Initialize();

            var id = NewId();
            var table = configuration.GetDesignFor<Entity>().Table;
            await store.InsertAsync(table, id, new
            {
                AwaitsReprojection = false,
                Discriminator = typeof(Entity).AssemblyQualifiedName,
                Version = 1,
                Document = configuration.Serializer.Serialize(new Entity())
            });

            new DocumentMigrationRunner().Run(fakeStore).Wait();

            A.CallTo(() => fakeStore.GetAsync(table, id)).MustNotHaveHappened();

            var row = await store.GetAsync(table, id);
            row[table.VersionColumn].ShouldBe(2);
        }

        [Fact]
        public async Task QueriesInSetsAndUpdatesOneByOne()
        {
            var fakeStore = A.Fake<IDocumentStore>(x => x.Wrapping(store));

            Document<Entity>().With(x => x.Number);

            store.Initialize();

            var documentTable = new DocumentTable("Entities");

            for (var i = 0; i < 200; i++)
            {
                await store.InsertAsync(documentTable, NewId(), new
                {
                    Discriminator = typeof(Entity).AssemblyQualifiedName,
                    Version = 0,
                    Document = configuration.Serializer.Serialize(new Entity())
                });
            }

            // bump the version of the configuration
            UseMigrations(new InlineMigration(1));

            new DocumentMigrationRunner().Run(fakeStore).Wait();

            // 1+2: Entities table => 100 rows
            // 3: Entities table => 0 rows
            A.CallTo(fakeStore)
                .Where(x => x.Method.Name == "Query")
                .WhenArgumentsMatch(x => x.Get<DocumentTable>(0).Name == "Entities")
                .MustHaveHappened(3, Times.Exactly);

            // each document is being updated individually
            A.CallTo(fakeStore)
                .Where(x => x.Method.Name == nameof(IDocumentStore.ExecuteAsync))
                .WhenArgumentsMatch(x => x.Get<DatabaseCommand[]>(0)[0] is UpdateCommand)
                .MustHaveHappened(200, Times.Exactly);
        }

        [Fact]
        public async Task AcceptsConcurrentWrites()
        {
            UseRealTables();

            Document<Entity>().With(x => x.Number);

            store.Initialize();

            var id = NewId();
            var table = new DocumentTable("Entities");
            var etag = await store.InsertAsync(table, id, new
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

            await store.UpdateAsync(table, id, etag, new { });

            gate2.WaitOne();

            failed.ShouldBe(false);
        }

        [Fact]
        public void DoesNotStartBackgroundProcessWhenTurnedOff()
        {
            DisableDocumentMigrationsInBackground();
            Document<Entity>().With(x => x.Number);

            store.Initialize();

            new DocumentMigrationRunner().Run(store).Wait();

            store.NumberOfRequests.ShouldBe(0);
        }

        [Fact]
        public void DoesNotStartBackgroundProcessWhenAllMigrationsAreTurnedOff()
        {
            DisableMigrations();
            Document<Entity>().With(x => x.Number);

            store.Initialize();

            new DocumentMigrationRunner().Run(store).Wait();

            store.NumberOfRequests.ShouldBe(0);
        }

        [Fact]
        public async Task ContinuesIfMigrationFails()
        {
            Document<Entity>().With(x => x.Number);

            store.Initialize();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                await session.SaveChangesAsync();
            }

            Reset();
            Document<Entity>().With(x => x.Number);

            var logEventSink = new ListSink();
            UseLogger(new LoggerConfiguration().WriteTo.Sink(logEventSink).CreateLogger());

            store.Initialize();

            UseMigrations(new InlineMigration(1, new ChangeDocument<Entity>((x, y) =>
            {
                throw new Exception();
            })));

            Should.NotThrow(() =>
            {
                new DocumentMigrationRunner().Run(store).Wait(1000);
            });

            logEventSink.Stop();

            var numberOfRetries = logEventSink.Captures
                .Count(x => x == $"Error while migrating document of type \"HybridDb.Tests.HybridDbTests+Entity\" with id \"{id}\".");

            // it has a back off of 100ms
            numberOfRetries.ShouldBeLessThan(12);
            numberOfRetries.ShouldBeGreaterThan(8);
        }

        [Fact]
        public async Task BacksUpMigratedDocumentBeforeMigration()
        {
            var id = NewId();
            Document<Entity>();

            store.Initialize();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChangesAsync();
            }

            Reset();
            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "1"; })));

            var backupWriter = new FakeBackupWriter();
            UseBackupWriter(backupWriter);

            store.Initialize();

            backupWriter.Files.Count.ShouldBe(1);
            backupWriter.Files[$"HybridDb.Tests.HybridDbTests+Entity_{id}_0.bak"]
                .ShouldBe(configuration.Serializer.Serialize(new Entity { Id = id, Property = "Asger" }));
        }


        public class ListSink : ILogEventSink
        {
            bool stopped = false;

            public ListSink()
            {
                Captures = new List<string>();
            }

            public List<string> Captures { get; set; }

            // to avoid mutating the list while still adding to it.
            public void Stop()
            {
                stopped = true;
            }

            public void Emit(LogEvent logEvent)
            {
                if (stopped) return;

                Captures.Add(logEvent.RenderMessage());
            }
        }
    }
}