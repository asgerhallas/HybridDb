using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using HybridDb.Config;
using HybridDb.Logging;
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

            new DocumentMigrationRunner(store).RunSynchronously();

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
            new DocumentMigrationRunner(store).RunSynchronously();

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

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity() { Id = id });
                session.SaveChanges();
            }

            Reset();
            Document<Entity>().With(x => x.Number);

            var capturingLogger = new CapturingLogger(store.Configuration.Logger);
            UseLogger(capturingLogger);

            InitializeStore();

            UseMigrations(new InlineMigration(1, new ChangeDocument<Entity>((x, y) =>
            {
                throw new Exception();
            })));
            
            Should.NotThrow(() =>
            {
                new DocumentMigrationRunner(store).RunInBackground().Wait(1000);
            });

            var numberOfRetries = capturingLogger.Captures.Count(x => x == string.Format("Error while migrating document with id HybridDb.Tests.HybridDbTests+Entity/{0}.", id));
            
            // it has a back off of 100ms
            numberOfRetries.ShouldBeLessThan(11);
            numberOfRetries.ShouldBeGreaterThan(9);
        }

        public class CapturingLogger : ILogger
        {
            readonly ILogger logger;

            public CapturingLogger(ILogger logger)
            {
                this.logger = logger;

                Captures = new List<string>();
            }

            public List<string> Captures { get; set; }

            public void Debug(string message, params object[] objs)
            {
                logger.Debug(message, objs);
            }

            public void Info(string message, params object[] objs)
            {
                logger.Info(message, objs);
            }

            public void Warn(string message, params object[] objs)
            {
                logger.Warn(message, objs);
            }

            public void Error(string message, params object[] objs)
            {
                logger.Error(message, objs);
                Captures.Add(string.Format(message, objs));
            }

            public void Error(string message, Exception exception, params object[] objs)
            {
                logger.Error(message, exception, objs);
                Captures.Add(string.Format(message, objs));
            }
        }
    }
}