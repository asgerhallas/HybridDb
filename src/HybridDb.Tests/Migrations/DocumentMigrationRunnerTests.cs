using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    public class DocumentMigrationRunnerTests  : HybridDbStoreTests
    {
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
                Discriminator = "Entity",
                Version = 0,
                Document = configuration.Serializer.Serialize(new Entity { Number = 42 })
            });

            new DocumentMigrationRunner(store).RunSynchronously();

            var row = store.Get(table, id);
            row["Number"].ShouldBe(result);
            row["AwaitsReprojection"].ShouldBe(false);
            row[table.VersionColumn].ShouldBe(0);
        }

        [Fact]
        public void DoesNotRetrieveDocumentIfNoReprojectionOrMigrationIsNeededButUpdatesVersion()
        {
            Document<Entity>().With(x => x.Number);
            Document<OtherEntity>().With(x => x.Number);

            // add migration for same version and one for other document
            UseMigrations(
                new InlineMigration(1, new ChangeDocument<Entity>((serializer, bytes) => bytes)),
                new InlineMigration(2, new ChangeDocument<OtherEntity>((serializer, bytes) => bytes)));

            var id = NewId();
            var table = configuration.GetDesignFor<Entity>().Table;
            store.Insert(table, id, new
            {
                AwaitsReprojection = false,
                Discriminator = "Entity",
                Version = 1,
                Document = configuration.Serializer.Serialize(new Entity())
            });

            var counter = new TracingDocumentStoreDecorator(store);
            
            new DocumentMigrationRunner(counter).RunSynchronously();

            var row = store.Get(table, id);
            counter.Gets.ShouldBeEmpty();
            row[table.VersionColumn].ShouldBe(2);
        }

        [Fact]
        public void QueriesInSetsAndUpdatesOneByOne()
        {
            Document<Entity>().With(x => x.Number);

            for (int i = 0; i < 200; i++)
            {
                store.Insert(new DocumentTable("Entities"), NewId(), new
                {
                    Discriminator = "Entity", 
                    Version = 0, 
                    Document = configuration.Serializer.Serialize(new Entity())
                });
            }

            // bump the version of the configuration
            UseMigrations(new InlineMigration(1));

            var counter = new TracingDocumentStoreDecorator(store);

            new DocumentMigrationRunner(counter).RunSynchronously();

            // 3 query for documents below version, first two return 100 rows each, last returns 0
            counter.Queries.Count.ShouldBe(3);
            
            // each document is being updated individually
            counter.Updates.Count.ShouldBe(200);
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

            new DocumentMigrationRunner(store)
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
        public void DoesNotStartBackgroundProcessWhenTurnedOff()
        {
            InitializeStore();

            DisableDocumentMigrationsInBackground();
            Document<Entity>().With(x => x.Number);
            
            new DocumentMigrationRunner(store).RunInBackground().Wait();

            store.NumberOfRequests.ShouldBe(0);
        }

        [Fact]
        public void ContinuesIfMigrationFails()
        {
            Document<Entity>().With(x => x.Number);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                session.SaveChanges();
            }

            Reset();
            Document<Entity>().With(x => x.Number);

            var logEventSink = new ListSink();
            UseLogger(new LoggerConfiguration().WriteTo.Sink(logEventSink).CreateLogger());

            InitializeStore();

            UseMigrations(new InlineMigration(1, new ChangeDocument<Entity>((x, y) =>
            {
                throw new Exception();
            })));

            Should.NotThrow(() =>
            {
                new DocumentMigrationRunner(store).RunInBackground().Wait(1000);
            });

            var numberOfRetries = logEventSink.Captures.Count(x => x == string.Format("Error while migrating document of type \"HybridDb.Tests.HybridDbTests+Entity\" with id \"{0}\".", id));
            
            // it has a back off of 100ms
            numberOfRetries.ShouldBeLessThan(11);
            numberOfRetries.ShouldBeGreaterThan(9);
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

            Reset();
            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "1"; })));

            var backupWriter = new FakeBackupWriter();
            UseBackupWriter(backupWriter);

            InitializeStore();

            backupWriter.Files.Count.ShouldBe(1);
            backupWriter.Files[string.Format("HybridDb.Tests.HybridDbTests+Entity_{0}_0.bak", id)]
                .ShouldBe(configuration.Serializer.Serialize(new Entity { Id = id, Property = "Asger" }));
        }


        public class ListSink : ILogEventSink
        {
            public ListSink()
            {
                Captures = new List<string>();
            }

            public List<string> Captures { get; set; }

            public void Emit(LogEvent logEvent)
            {
                Captures.Add(logEvent.RenderMessage());
            }
        }
    }
}