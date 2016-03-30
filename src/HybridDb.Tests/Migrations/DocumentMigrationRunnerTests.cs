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
    public class DocumentMigrationRunnerTests : HybridDbTests
    {
        [Theory]
        [InlineData(true, 42)]
        [InlineData(false, 0)]
        public void ReprojectsWhenAwaitingReprojection(bool awaitsReprojection, int result)
        {
            Document<Entity>().With(x => x.Number);

            store.Initialize();

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

            store.Initialize();

            var id = NewId();
            var table = configuration.GetDesignFor<Entity>().Table;
            store.Insert(table, id, new
            {
                AwaitsReprojection = false,
                Discriminator = typeof(Entity).AssemblyQualifiedName,
                Version = 1,
                Document = configuration.Serializer.Serialize(new Entity())
            });

            var counter = new TracingDocumentStoreDecorator(store);

            new DocumentMigrationRunner().Run(counter).Wait();

            var row = store.Get(table, id);
            counter.Gets.ShouldBeEmpty();
            row[table.VersionColumn].ShouldBe(2);
        }

        [Fact]
        public void QueriesInSetsAndUpdatesOneByOne()
        {
            Document<Entity>().With(x => x.Number);

            store.Initialize();

            for (int i = 0; i < 200; i++)
            {
                store.Insert(new DocumentTable("Entities"), NewId(), new
                {
                    Discriminator = typeof(Entity).AssemblyQualifiedName, 
                    Version = 0, 
                    Document = configuration.Serializer.Serialize(new Entity())
                });
            }

            // bump the version of the configuration
            UseMigrations(new InlineMigration(1));

            var counter = new TracingDocumentStoreDecorator(store);

            new DocumentMigrationRunner().Run(counter).Wait();

            // 1+2: Entities table => 100 rows
            // 3: Entities table => 0 rows
            counter.Queries.Count(x => x.Name == "Entities").ShouldBe(3);
            
            // each document is being updated individually
            counter.Updates.Count.ShouldBe(200);
        }

        [Fact]
        public void AcceptsConcurrentWrites()
        {
            UseRealTables();

            Document<Entity>().With(x => x.Number);

            store.Initialize();

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
        public void ContinuesIfMigrationFails()
        {
            Document<Entity>().With(x => x.Number);

            store.Initialize();

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

            store.Initialize();

            UseMigrations(new InlineMigration(1, new ChangeDocument<Entity>((x, y) =>
            {
                throw new Exception();
            })));

            Should.NotThrow(() =>
            {
                new DocumentMigrationRunner().Run(store).Wait(1000);
            });

            var numberOfRetries = logEventSink.Captures.Count(x => x == $"Error while migrating document of type \"HybridDb.Tests.HybridDbTests+Entity\" with id \"{id}\".");
            
            // it has a back off of 100ms
            numberOfRetries.ShouldBeLessThan(12);
            numberOfRetries.ShouldBeGreaterThan(8);
        }

        [Fact]
        public void BacksUpMigratedDocumentBeforeMigration()
        {
            var id = NewId();
            Document<Entity>();

            store.Initialize();

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

            store.Initialize();

            backupWriter.Files.Count.ShouldBe(1);
            backupWriter.Files[$"HybridDb.Tests.HybridDbTests+Entity_{id}_0.bak"]
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