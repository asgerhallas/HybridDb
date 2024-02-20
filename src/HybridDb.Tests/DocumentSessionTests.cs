using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FakeItEasy;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq.Old;
using HybridDb.Migrations.Documents;
using HybridDb.Queue;
using Microsoft.Data.SqlClient;
using ShinySwitch;
using ShouldBeLike;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using SqlCommand = HybridDb.Commands.SqlCommand;

namespace HybridDb.Tests
{
    public class DocumentSessionTests : HybridDbTests
    {
        public DocumentSessionTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void CanEvictEntity()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            var entity1 = new Entity { Id = id, Property = "Asger" };
            session.Store(entity1);
            session.Advanced.TryGetManagedEntity<Entity>(id, out _).ShouldBe(true);
            session.Advanced.Evict(entity1);
            session.Advanced.TryGetManagedEntity<Entity>(id, out _).ShouldBe(false);
        }

        [Fact]
        public void CanGetEtagForEntity()
        {
            Document<Entity>();

            using var session = store.OpenSession();

            var entity1 = new Entity { Id = NewId(), Property = "Asger" };
            session.Advanced.GetEtagFor(entity1).ShouldBe(null);

            session.Store(entity1);
            session.Advanced.GetEtagFor(entity1).ShouldBe(null);

            session.SaveChanges();
            session.Advanced.GetEtagFor(entity1).ShouldNotBe(null);
            session.Advanced.GetEtagFor(entity1).ShouldNotBe(Guid.Empty);
        }

        [Fact]
        public void CanDeferCommands()
        {
            Document<Entity>();

            var table = store.Configuration.GetDesignFor<Entity>().Table;
            var id = NewId();

            using (var session = store.OpenSession())
            {
                session.Advanced.Defer(new InsertCommand(table, id, new { }));
                store.Stats.NumberOfCommands.ShouldBe(0);
                session.SaveChanges();
                store.Stats.NumberOfCommands.ShouldBe(1);
            }

            var entity = store.Query(table, out _, where: $"Id = '{id}'").SingleOrDefault();
            Assert.NotNull(entity);
        }

        [Fact]
        public void CanDeferCommands_AreClearedOnSaveChanges()
        {
            configuration.UseMessageQueue();

            Document<Entity>();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            using var session = store.OpenSession();

            session.Advanced.Defer(new InsertCommand(table, NewId(), new { }));
            session.Enqueue(new object());

            session.Advanced.DeferredCommands.Count.ShouldBe(2);

            session.SaveChanges();

            session.Advanced.DeferredCommands.ShouldBeEmpty();
        }

        [Fact]
        public void CanOpenSession() => store.OpenSession().ShouldNotBe(null);

        [Fact]
        public void CanStoreDocument()
        {
            Document<Entity>();
            var table = store.Configuration.GetDesignFor<Entity>().Table;

            using (var session = store.OpenSession())
            {
                session.Store(new Entity());
                session.SaveChanges();
            }

            var entity = store.Query(table, out _).SingleOrDefault();
            Assert.NotNull(entity);
            Assert.NotNull(entity["Document"]);
            Assert.NotEqual(0, ((string)entity["Document"]).Length);
        }

        [Fact]
        public void AccessToNestedPropertyCanBeNullSafe()
        {
            Document<Entity>().Column(x => x.TheChild.NestedProperty);

            using (var session = store.OpenSession())
            {
                session.Store(
                    new Entity
                    {
                        TheChild = null
                    });

                session.SaveChanges();
            }

            var table = store.Configuration.GetDesignFor<Entity>().Table;
            var entity = store.Query(table, out _).SingleOrDefault();
            Assert.True(entity.Keys.Contains("TheChildNestedProperty"));
            Assert.Null(entity["TheChildNestedProperty"]);
        }

        [Fact]
        public void AccessToNestedPropertyThrowsIfNotMadeNullSafe()
        {
            using var session = store.OpenSession();

            session.Store(
                new Entity
                {
                    TheChild = null
                });

            Should.Throw<TargetInvocationException>(() => session.SaveChanges());
        }

        [Fact]
        public void StoresIdRetrievedFromObject()
        {
            Document<Entity>();

            var id = NewId();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                session.SaveChanges();
            }

            var table = store.Configuration.GetDesignFor<Entity>().Table;
            var retrievedId = store.Query(table, out _, select: "Id").SingleOrDefault();

            retrievedId["Id"].ShouldBe(id);
        }

        [Fact]
        public void SessionWillNotSaveWhenSaveChangesIsNotCalled()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            var entity = new Entity { Id = id, Property = "Asger" };
            session.Store(entity);
            session.SaveChanges();
            entity.Property = "Lars";
            session.Advanced.Clear();
            session.Load<Entity>(id).Property.ShouldBe("Asger");
        }

        [Fact]
        public void CanSaveChangesWithPessimisticConcurrency()
        {
            Document<Entity>();

            var id = NewId();

            using var session1 = store.OpenSession();

            using var session2 = store.OpenSession();

            var entityFromSession1 = new Entity { Id = id, Property = "Asger" };
            session1.Store(entityFromSession1);
            session1.SaveChanges();

            var entityFromSession2 = session2.Load<Entity>(id);
            entityFromSession2.Property = " er craazy";
            session2.SaveChanges();

            entityFromSession1.Property += " er 4 real";
            session1.SaveChanges(true, false);

            session1.Advanced.Clear();
            session1.Load<Entity>(id).Property.ShouldBe("Asger er 4 real");
        }

        [Fact]
        public void CanUpdateDocument()
        {
            Document<Entity>();

            var id = NewId();

            using var session1 = store.OpenSession();

            var entity1 = new Entity { Id = id, Property = "Asger" };
            session1.Store(entity1);
            session1.SaveChanges();
            session1.Advanced.Clear();

            var entity2 = new Entity { Id = id, Property = "Peter" };
            session1.Store(entity2, null);
            session1.SaveChanges();
            session1.Advanced.Clear();

            session1.Load<Entity>(id).Property.ShouldBe("Peter");
        }

        [Fact]
        public void CanDeleteDocument()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Load<Entity>(id);
            session.Delete(entity);
            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>(id).ShouldBe(null);
        }

        [Fact]
        public void DeletingATransientEntityRemovesItFromSession()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            var entity = new Entity { Id = id, Property = "Asger" };
            session.Store(entity);
            session.Delete(entity);
            session.Advanced.TryGetManagedEntity<Entity>(id, out _).ShouldBe(false);
        }

        [Fact]
        public void DeletingAnEntityNotLoadedInSessionIsIgnored()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            var entity = new Entity { Id = id, Property = "Asger" };
            Should.NotThrow(() => session.Delete(entity));
            session.Advanced.TryGetManagedEntity<Entity>(id, out _).ShouldBe(false);
        }

        [Fact]
        public void LoadingADocumentMarkedForDeletionReturnNothing()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Load<Entity>(id);
            session.Delete(entity);
            session.Load<Entity>(id).ShouldBe(null);
        }

        [Fact]
        public void LoadingAManagedDocumentWithWrongTypeReturnsNothing()
        {
            Document<Entity>();
            Document<OtherEntity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger" });
            var otherEntity = session.Load<OtherEntity>(id);
            otherEntity.ShouldBe(null);
        }

        [Fact]
        public void CanClearSession()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger" });
            session.Advanced.Clear();
            session.Advanced.TryGetManagedEntity<Entity>(id, out _).ShouldBe(false);
        }

        [Fact]
        public void CanClearSession_DeferredCommands()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Advanced.Defer(new DeleteCommand(configuration.GetDesignFor<Entity>().Table, id, Guid.Empty));
            session.Advanced.Clear();
            session.Advanced.DeferredCommands.ShouldBeEmpty();
        }

        [Fact]
        public void CanClearSession_Transaction()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            using var tx = store.BeginTransaction();
            session.Advanced.Enlist(tx);
            session.Advanced.Clear();
            session.Advanced.DocumentTransaction.ShouldBe(null);
        }

        [Fact]
        public void CanClearSession_SessionData()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Advanced.SessionData["Doomed"] = 1337;

            session.Advanced.Clear();

            session.Advanced.SessionData.ShouldNotContainKey("Doomed");
        }

        [Fact]
        public void CanCheckIfEntityIsLoadedInSession()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Advanced.TryGetManagedEntity<Entity>(id, out _).ShouldBe(false);
            session.Store(new Entity { Id = id, Property = "Asger" });
            session.Advanced.TryGetManagedEntity<Entity>(id, out _).ShouldBe(true);
        }

        [Fact]
        public void CanLoadDocument()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Load<Entity>(id);
            entity.Property.ShouldBe("Asger");
        }

        [Fact]
        public void CanLoadDocumentWithDesign()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id });
            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Load(typeof(Entity), id);

            entity.ShouldBeOfType<Entity>();
        }

        [Fact]
        public void LoadingSameDocumentTwiceInSameSessionReturnsSameInstance()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger" });
            session.SaveChanges();
            session.Advanced.Clear();

            var document1 = session.Load<Entity>(id);
            var document2 = session.Load<Entity>(id);
            document1.ShouldBe(document2);
        }

        [Fact]
        public void CanLoadATransientEntity()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            var entity = new Entity { Id = id, Property = "Asger" };
            session.Store(entity);
            session.Load<Entity>(id).ShouldBe(entity);
        }

        [Fact]
        public void LoadingANonExistingDocumentReturnsNull()
        {
            Document<Entity>();

            using var session = store.OpenSession();

            session.Load<Entity>(NewId()).ShouldBe(null);
        }

        [Fact]
        public void SavesChangesWhenObjectHasChanged()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger" });
            session.SaveChanges();
            session.Advanced.Clear();

            var document = session.Load<Entity>(id);
            document.Property = "Lars";
            session.SaveChanges();

            store.Stats.NumberOfCommands.ShouldBe(2);
            session.Advanced.Clear();
            session.Load<Entity>(id).Property.ShouldBe("Lars");
        }

        [Fact]
        public void DoesNotSaveChangesWhenObjectHasNotChanged()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger" });
            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>(id);
            session.SaveChanges(); // Should not issue a request

            store.Stats.NumberOfCommands.ShouldBe(1);
        }

        [Fact]
        public void SavingChangesTwiceInARowDoesNothing()
        {
            Document<Entity>();

            var id1 = NewId();
            var id2 = NewId();
            var id3 = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id1, Property = "Asger" });
            session.Store(new Entity { Id = id2, Property = "Asger" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entity1 = session.Load<Entity>(id1);
            var entity2 = session.Load<Entity>(id2);
            session.Delete(entity1);
            entity2.Property = "Lars";
            session.Store(new Entity { Id = id3, Property = "Jacob" });
            session.SaveChanges();

            var numberOfRequests = store.Stats.NumberOfRequests;
            var lastEtag = store.Stats.LastWrittenEtag;

            session.SaveChanges();

            store.Stats.NumberOfRequests.ShouldBe(numberOfRequests);
            store.Stats.LastWrittenEtag.ShouldBe(lastEtag);
        }

        [Fact]
        public void IssuesConcurrencyExceptionWhenTwoSessionsChangeSameDocument()
        {
            Document<Entity>();

            var id1 = NewId();

            using var session1 = store.OpenSession();

            using var session2 = store.OpenSession();

            session1.Store(new Entity { Id = id1, Property = "Asger" });
            session1.SaveChanges();

            var entity1 = session1.Load<Entity>(id1);
            var entity2 = session2.Load<Entity>(id1);

            entity1.Property = "A";
            session1.SaveChanges();

            entity2.Property = "B";
            Should.Throw<ConcurrencyException>(() => session2.SaveChanges());
        }

        [Fact]
        public void CanQueryDocument()
        {
            Document<Entity>().Column(x => x.ProjectedProperty);

            using var session = store.OpenSession();

            session.Store(new Entity { Id = NewId(), Property = "Asger", ProjectedProperty = "Large" });
            session.Store(new Entity { Id = NewId(), Property = "Lars", ProjectedProperty = "Small" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entities = session.Query<Entity>().Where(x => x.ProjectedProperty == "Large").ToList();

            entities.Count.ShouldBe(1);
            entities[0].Property.ShouldBe("Asger");
            entities[0].ProjectedProperty.ShouldBe("Large");
        }

        [Fact]
        public void Bug_Query_MissingEscape()
        {
            Document<EntityWithDateTimeOffset>().Column(x => x.From);

            using var session = store.OpenSession();

            session.Store(
                new EntityWithDateTimeOffset
                {
                    Id = NewId(),
                    Property = "Asger",
                    From = new DateTimeOffset(new DateTime(2001, 12, 1))
                });

            session.Store(
                new EntityWithDateTimeOffset
                {
                    Id = NewId(),
                    Property = "Lars",
                    From = new DateTimeOffset(new DateTime(2001, 12, 2))
                });

            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Query<EntityWithDateTimeOffset>()
                .OrderByDescending(x => x.From)
                .First(x => x.From != null);

            entity.Property.ShouldBe("Lars");
        }

        [Fact]
        public void Bug_Update_MissingEscape()
        {
            Document<EntityWithDateTimeOffset>().Column(x => x.From);

            using var session = store.OpenSession();

            var newId = NewId();
            session.Store(
                new EntityWithDateTimeOffset
                {
                    Id = newId,
                    Property = "Asger",
                    From = new DateTimeOffset(new DateTime(2001, 12, 1))
                });

            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Load<EntityWithDateTimeOffset>(newId);

            entity.Property = "Danny";

            session.SaveChanges();
        }

        [Fact]
        public void CanSaveChangesOnAQueriedDocument()
        {
            Document<Entity>().Column(x => x.ProjectedProperty);

            var id = NewId();
            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Query<Entity>().Single(x => x.ProjectedProperty == "Large");

            entity.Property = "Lars";
            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>(id).Property.ShouldBe("Lars");
        }

        [Fact]
        public void QueryingALoadedDocumentReturnsSameInstance()
        {
            Document<Entity>().Column(x => x.ProjectedProperty);

            var id = NewId();
            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large" });
            session.SaveChanges();
            session.Advanced.Clear();

            var instance1 = session.Load<Entity>(id);
            var instance2 = session.Query<Entity>().Single(x => x.ProjectedProperty == "Large");

            instance1.ShouldBe(instance2);
        }

        [Fact]
        public void QueryingALoadedDocumentMarkedForDeletionReturnsNothing()
        {
            Document<Entity>().Column(x => x.ProjectedProperty);

            var id = NewId();
            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Load<Entity>(id);
            session.Delete(entity);

            var entities = session.Query<Entity>().Where(x => x.ProjectedProperty == "Large").ToList();

            entities.Count.ShouldBe(0);
        }

        [Fact]
        public void CanQueryAndReturnProjection()
        {
            Document<Entity>()
                .Column(x => x.ProjectedProperty)
                .Column(x => x.TheChild.NestedProperty);

            using var session = store.OpenSession();

            session.Store(new Entity { Id = NewId(), Property = "Asger", ProjectedProperty = "Large", TheChild = new Entity.Child { NestedProperty = "Hans" } });
            session.Store(new Entity { Id = NewId(), Property = "Lars", ProjectedProperty = "Small", TheChild = new Entity.Child { NestedProperty = "Peter" } });
            session.SaveChanges();
            session.Advanced.Clear();

            var entities = session.Query<Entity>().Where(x => x.ProjectedProperty == "Large").AsProjection<EntityProjection>().ToList();

            entities.Count.ShouldBe(1);
            entities[0].ProjectedProperty.ShouldBe("Large");
            entities[0].TheChildNestedProperty.ShouldBe("Hans");
        }

        [Fact]
        public void WillNotSaveChangesToProjectedValues()
        {
            Document<Entity>()
                .Column(x => x.ProjectedProperty)
                .Column(x => x.TheChild.NestedProperty);

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Query<Entity>().AsProjection<EntityProjection>().Single(x => x.ProjectedProperty == "Large");
            entity.ProjectedProperty = "Small";
            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>(id).ProjectedProperty.ShouldBe("Large");
        }

        [Fact]
        public void StoreOneTypeAndLoadAnotherFromSameTableAndKeyResultsInOneManagedEntity()
        {
            using var session = store.OpenSession();

            session.Store("key", new OtherEntity());
            session.SaveChanges();

            session.Load<object>("key").ShouldNotBe(null);

            session.Advanced.ManagedEntities.Count().ShouldBe(1);
        }

        [Fact]
        public void StoreTwoInstancesToSameTableWithSameId_Throws()
        {
            Document<DerivedEntity>();
            Document<MoreDerivedEntity1>();

            using var session = store.OpenSession();

            session.Store("key", new MoreDerivedEntity1());

            Should.Throw<HybridDbException>(() => session.Store("key", new DerivedEntity()));
        }

        [Fact]
        public void CanQueryUserDefinedProjection()
        {
            Document<OtherEntity>().Column(x => 2, x => x.Name("Unknown"));

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new OtherEntity { Id = id });
            session.SaveChanges();

            var entities = session.Query<OtherEntity>().Where(x => x.Column<int>("Unknown") == 2).ToList();
            entities.Count.ShouldBe(1);
        }

        [Fact]
        public void Bug10_UsesProjectedTypeToSelectColumns()
        {
            Document<Entity>()
                .Column(x => x.Property)
                .Column(x => x.Number);

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id, Property = "asger", Number = 2 });
            session.SaveChanges();

            Should.NotThrow(() => session.Query<Entity>().Select(x => new AProjection { Number = x.Number }).ToList());
        }

        [Fact]
        public void CanQueryIndex()
        {
            Document<AbstractEntity>()
                .Column(x => x.Number, x => x.Name(nameof(EntityIndex.YksiKaksiKolme)))
                .Column(x => x.Property);

            Document<MoreDerivedEntity1>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new MoreDerivedEntity1 { Id = id, Property = "Asger", Number = 2 });
            session.SaveChanges();

            //var test = from x in session.Query<AbstractEntity>()
            //           let index = x.Index<EntityIndex>()
            //           where x.Property == "Asger" && index.YksiKaksiKolme > 1
            //           select x;

            var entity = session.Query<AbstractEntity>().Single(x => x.Property == "Asger" && x.Index<EntityIndex>().YksiKaksiKolme > 1);
            entity.ShouldBeOfType<MoreDerivedEntity1>();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AppliesMigrationsOnLoad(bool disableDocumentMigrationsOnStartup)
        {
            Document<Entity>();

            var id = NewId();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges();
            }

            ResetConfiguration();

            Document<Entity>();

            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] = "Peter"; })));

            if (disableDocumentMigrationsOnStartup) DisableBackgroundMigrations();

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>(id);
                entity.Property.ShouldBe("Peter");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AppliesMigrationsOnQuery(bool disableDocumentMigrationsOnStartup)
        {
            Document<AbstractEntity>().Column(x => x.Number);
            Document<DerivedEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Property = "Asger", Number = 1 });
                session.Store(new MoreDerivedEntity1 { Property = "Jacob", Number = 2 });
                session.Store(new MoreDerivedEntity2 { Property = "Lars", Number = 3 });
                session.SaveChanges();
            }

            ResetConfiguration();

            Document<AbstractEntity>().Column(x => x.Number);
            Document<DerivedEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            UseMigrations(
                new InlineMigration(
                    1,
                    new ChangeDocumentAsJObject<AbstractEntity>(x => { x["Property"] = x["Property"] + " er cool"; }),
                    new ChangeDocumentAsJObject<MoreDerivedEntity2>(x => { x["Property"] = x["Property"] + "io"; })));

            if (disableDocumentMigrationsOnStartup) DisableBackgroundMigrations();

            using (var session = store.OpenSession())
            {
                var rows = session.Query<AbstractEntity>().OrderBy(x => x.Number).ToList();

                rows[0].Property.ShouldBe("Asger er cool");
                rows[1].Property.ShouldBe("Jacob er cool");
                rows[2].Property.ShouldBe("Lars er coolio");
            }
        }

        [Fact]
        public void TracksChangesFromMigrations()
        {
            Document<Entity>();

            var id = NewId();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges();
            }

            ResetConfiguration();

            Document<Entity>();

            TouchStore();

            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] = "Peter"; })));

            var numberOfCommands = store.Stats.NumberOfCommands;

            using (var session = store.OpenSession())
            {
                session.Load<Entity>(id);
                session.SaveChanges();
            }

            (store.Stats.NumberOfCommands - numberOfCommands).ShouldBe(1);
        }

        [Fact]
        public void AddsVersionOnInsert()
        {
            Document<Entity>();
            UseMigrations(new InlineMigration(1));

            var id = NewId();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                session.SaveChanges();
            }

            var table = configuration.GetDesignFor<Entity>().Table;
            var row = store.Get(table, id);
            ((int)row[DocumentTable.VersionColumn]).ShouldBe(1);
        }

        [Fact]
        public void UpdatesVersionOnUpdate()
        {
            Document<Entity>();

            var id = NewId();
            var table = configuration.GetDesignFor<Entity>().Table;

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                session.SaveChanges();
            }

            ResetConfiguration();
            Document<Entity>();

            TouchStore();

            UseMigrations(new InlineMigration(1), new InlineMigration(2));

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>(id);
                entity.Number++;
                session.SaveChanges();
            }

            var row = store.Get(table, id);
            ((int)row[DocumentTable.VersionColumn]).ShouldBe(2);
        }

        [Fact]
        public void BacksUpMigratedDocumentOnSave()
        {
            var id = NewId();

            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges();
            }

            var backupWriter = new FakeBackupWriter();

            ResetConfiguration();

            UseBackupWriter(backupWriter);
            Document<Entity>();
            DisableBackgroundMigrations();
            UseMigrations(
                new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "1"; })));

            using (var session = store.OpenSession())
            {
                session.Load<Entity>(id);

                // no backup on load
                backupWriter.Files.Count.ShouldBe(0);

                session.SaveChanges();

                // backup on save
                backupWriter.Files.Count.ShouldBe(1);
                backupWriter.Files[$"HybridDb.Tests.HybridDbTests+Entity_{id}_0.bak"]
                    .ShouldBe(Encoding.UTF8.GetBytes(configuration.Serializer.Serialize(new Entity { Id = id, Property = "Asger" })));
            }

            // try it again
            ResetConfiguration();

            UseBackupWriter(backupWriter);
            Document<Entity>();
            DisableBackgroundMigrations();
            UseMigrations(
                new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "1"; })),
                new InlineMigration(2, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "2"; })));

            using (var session = store.OpenSession())
            {
                session.Load<Entity>(id);

                // no backup on load
                backupWriter.Files.Count.ShouldBe(1);

                session.SaveChanges();

                // backup on save
                backupWriter.Files.Count.ShouldBe(2);
                backupWriter.Files[$"HybridDb.Tests.HybridDbTests+Entity_{id}_1.bak"]
                    .ShouldBe(Encoding.UTF8.GetBytes(configuration.Serializer.Serialize(new Entity { Id = id, Property = "Asger1" })));
            }
        }

        [Fact]
        public void DoesNotBackUpDocumentWithNoMigrations()
        {
            Document<Entity>();

            var backupWriter = new FakeBackupWriter();
            UseBackupWriter(backupWriter);
            DisableBackgroundMigrations();

            var id = NewId();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>(id);

                entity.Property = "Jacob";

                session.SaveChanges();

                // no migrations, means no backup
                backupWriter.Files.Count.ShouldBe(0);
            }
        }

        [Fact]
        public void FailsOnSaveChangesWhenPreviousSaveFailed()
        {
            var fakeStore = A.Fake<IDocumentStore>(x => x.Wrapping(store));

            A.CallTo(fakeStore)
                .Where(x => x.Method.Name == nameof(IDocumentStore.BeginTransaction))
                .Throws(() => new Exception());

            Document<Entity>();

            // The this reference inside OpenSession() is not referencing the fake store, but the wrapped store itself.
            // Therefore we bypass the OpenSession factory.
            using var session = new DocumentSession(fakeStore, fakeStore.Configuration.Resolve<DocumentMigrator>());

            session.Store(new Entity());

            try
            {
                session.SaveChanges(); // fails when in an inconsitent state
            }
            catch (Exception)
            {
                // ignored
            }

            Should.Throw<InvalidOperationException>(() => session.SaveChanges())
                .Message.ShouldBe("Session is not in a valid state. Please dispose it and open a new one.");
        }

        [Fact]
        public void DoesNotFailsOnSaveChangesWhenPreviousSaveWasSuccessful()
        {
            Document<Entity>();

            using var session = store.OpenSession();

            session.Store(new Entity());

            session.SaveChanges();
            session.SaveChanges();
        }

        [Fact]
        public void DoesNotFailsOnSaveChangesWhenPreviousSaveWasNoop()
        {
            using var session = store.OpenSession();

            session.SaveChanges();
            session.SaveChanges();
        }

        [Fact]
        public void CanUseADifferentIdProjection()
        {
            Document<Entity>().Key(x => x.Property);

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Property = "TheId" });
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>("TheId");

                entity.ShouldNotBe(null);
            }
        }

        [Fact]
        public void CanStoreWithGivenKey()
        {
            Document<EntityWithoutId>();

            using (var session = store.OpenSession())
            {
                session.Store("mykey", new EntityWithoutId { Data = "TheId" });
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<EntityWithoutId>("mykey");

                entity.ShouldNotBe(null);
            }
        }

        [Fact]
        public void Store_NewInstance_WithSameKey()
        {
            Document<EntityWithoutId>();

            using var session = store.OpenSession();

            session.Store("mykey", new EntityWithoutId { Data = "1" });

            Should.Throw<HybridDbException>(() => session.Store("mykey", new EntityWithoutId { Data = "2" }))
                .Message.ShouldBe("Attempted to store a different object with id 'mykey'.");
        }

        [Fact]
        public void Store_SameInstance_WithSameKey()
        {
            Document<EntityWithoutId>();

            using var session = store.OpenSession();

            var entity = new EntityWithoutId { Data = "1" };
            session.Store("mykey", entity);

            var before = session.Advanced.ManagedEntities.Single();

            session.Store("mykey", entity);

            session.Advanced.ManagedEntities.Single()
                .ShouldBeLike(before);
        }

        [Fact]
        public void StoreNull_Noop()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store<object>(null);
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Query<Entity>().ToList();

                entity.ShouldBeEmpty();
            }
        }

        [Fact]
        public void CanStoreMetadata()
        {
            Document<Entity>();

            var id = NewId();

            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id };
                session.Store(entity);
                session.Advanced.SetMetadataFor(
                    entity,
                    new Dictionary<string, List<string>>
                    {
                        ["key"] = new() { "value1", "value2" }
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>(id);

                var metadata = session.Advanced.GetMetadataFor(entity);

                metadata["key"].ShouldBe(new List<string> { "value1", "value2" });
            }
        }

        [Fact]
        public void CanStoreWithoutOverwritingMetadata()
        {
            Document<Entity>();

            var id = NewId();

            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id };
                session.Store(entity);
                session.Advanced.SetMetadataFor(
                    entity,
                    new Dictionary<string, List<string>>
                    {
                        ["key"] = new() { "value1", "value2" }
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>(id);
                entity.Field = "a new field";
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>(id);

                var metadata = session.Advanced.GetMetadataFor(entity);

                metadata["key"].ShouldBe(new List<string> { "value1", "value2" });
            }
        }

        [Fact]
        public void CanUpdateMetadata()
        {
            Document<Entity>();

            var id = NewId();

            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id };
                session.Store(entity);
                session.Advanced.SetMetadataFor(
                    entity,
                    new Dictionary<string, List<string>>
                    {
                        ["key"] = new() { "value1", "value2" }
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>(id);
                session.Advanced.SetMetadataFor(
                    entity,
                    new Dictionary<string, List<string>>
                    {
                        ["another-key"] = new() { "value" }
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>(id);

                var metadata = session.Advanced.GetMetadataFor(entity);

                metadata.Keys.Count.ShouldBe(1);
                metadata["another-key"].ShouldBe(new List<string> { "value" });
            }
        }

        [Fact]
        public void CanSetMetadataToNull()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                var entity = new Entity();
                session.Store("id", entity);
                session.Advanced.SetMetadataFor(
                    entity,
                    new Dictionary<string, List<string>>
                    {
                        ["key"] = new() { "value1", "value2" }
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>("id");
                session.Advanced.SetMetadataFor(entity, null);
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>("id");

                var metadata = session.Advanced.GetMetadataFor(entity);

                metadata.ShouldBe(null);
            }
        }

        [Fact]
        public void UsesIdToStringAsDefaultKeyResolver()
        {
            using var session = store.OpenSession();

            var id = Guid.NewGuid();
            session.Store(
                new EntityWithFunnyKey
                {
                    Id = id
                });

            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<EntityWithFunnyKey>(id.ToString()).Id.ShouldBe(id);
        }

        [Fact]
        public void CanSetDefaultKeyResolver()
        {
            UseKeyResolver(x => "asger");

            using var session = store.OpenSession();

            session.Store(new Entity());

            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>("asger").ShouldNotBe(null);
        }

        [Fact]
        public void FailsIfStringProjectionIsTruncated()
        {
            Document<Entity>().JsonColumn(x => x.Children);

            using var session = store.OpenSession();

            session.Store(
                new Entity
                {
                    Field = "1234567890+"
                });

            Should.Throw<SqlException>(() => session.SaveChanges());
        }

        [Fact]
        public void CanProjectCollection()
        {
            Document<Entity>().JsonColumn(x => x.Children);

            var id = NewId();

            using (var session = store.OpenSession())
            {
                var entity1 = new Entity
                {
                    Id = id,
                    Children =
                    {
                        new Entity.Child { NestedProperty = "A" },
                        new Entity.Child { NestedProperty = "B" }
                    }
                };

                session.Store(entity1);
                session.SaveChanges();
            }

            var row = store.Get(store.Configuration.GetDesignFor<Entity>().Table, id);
            row["Children"]
                .ShouldBe("[{\"NestedDouble\":0.0,\"NestedProperty\":\"A\"},{\"NestedDouble\":0.0,\"NestedProperty\":\"B\"}]");
        }

        [Fact]
        public void CannotEnlistInForeignTransaction()
        {
            Document<Entity>();

            using var session = store.OpenSession();

            ResetStore();

            using var tx = store.BeginTransaction();

            Should.Throw<ArgumentException>(() => session.Advanced.Enlist(tx))
                .Message.ShouldBe("Cannot enlist in a transaction that does not originate from the same store as the session.");
        }

        [Fact]
        public void BaitAndSwitch()
        {
            Document<BaseCase>("Cases");
            Document<Case>();
            Document<PatchCase>();

            store.Configuration.AddEventHandler(
                x => Switch.On(x)
                    .Match<EntityLoaded>(
                        loaded =>
                        {
                            if (loaded.ManagedEntity.Entity is PatchCase patch)
                            {
                                var session2 = loaded.Session.Advanced.DocumentStore.OpenSession();

                                var @case = session2.Load<Case>(patch.ParentCaseId);

                                @case.Id = patch.Id;
                                @case.Name = patch.PatchedName;

                                var managedEntity = session2.Advanced.ManagedEntities.Values.Single();

                                loaded.ManagedEntity.Entity = managedEntity.Entity;
                                loaded.ManagedEntity.Design = managedEntity.Design;
                                loaded.ManagedEntity.Document = managedEntity.Document;
                                loaded.ManagedEntity.Etag = managedEntity.Etag;
                                loaded.ManagedEntity.Version = managedEntity.Version;
                            }
                        }));

            using var session = store.OpenSession();

            var caseId = NewId();

            session.Store(
                new Case
                {
                    Id = caseId,
                    Name = "Asger"
                });

            var patchCaseId = NewId();

            session.Store(
                new PatchCase
                {
                    Id = patchCaseId,
                    ParentCaseId = caseId,
                    PatchedName = "Lars"
                });

            session.SaveChanges();
            session.Advanced.Clear();

            var @case = session.Load<Case>(patchCaseId);
            @case.ShouldBeOfType<Case>();
            @case.Id.ShouldBe(patchCaseId);
            @case.Name.ShouldBe("Lars");

            var originalCase = session.Load<Case>(caseId);
            originalCase.ShouldBeOfType<Case>();
            originalCase.Id.ShouldBe(caseId);
            originalCase.Name.ShouldBe("Asger");
        }

        [Fact]
        public void CanQueryAndReturnProjectionUsingSqlBuilder()
        {
            var sql = new SqlBuilder();

            sql.Append(@"
                select '1.1' ProjectedProperty, '1.2' TheChildNestedProperty
                union
                select '2.1' ProjectedProperty, '2.2' TheChildNestedProperty
            ");

            using var session = store.OpenSession();

            var entities = session.Query<EntityProjection>(sql).ToList();

            entities.Count.ShouldBe(2);
            entities[0].ProjectedProperty.ShouldBe("1.1");
            entities[0].TheChildNestedProperty.ShouldBe("1.2");
            entities[1].ProjectedProperty.ShouldBe("2.1");
            entities[1].TheChildNestedProperty.ShouldBe("2.2");
        }

        [Fact]
        public void DeferredCommandsFirst()
        {
            Document<Entity>();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var tableName = store.Database.FormatTableNameAndEscape(table.Name);

            using var session = store.OpenSession();

            session.Advanced.Defer(new SqlCommand(new SqlBuilder($"truncate table {tableName}"), -1));

            session.Store(new Entity());

            session.SaveChanges();

            session.Query<Entity>().ToList().Count.ShouldBe(1);
        }

        public class BaseCase { }

        public class Case : BaseCase
        {
            public string Id { get; set; }

            public string Name { get; set; }
        }

        public class PatchCase : BaseCase
        {
            public string Id { get; set; }
            public string ParentCaseId { get; set; }

            public string PatchedName { get; set; }
        }

        public class EntityWithFunnyKey
        {
            public Guid Id { get; set; }
        }

        public class EntityProjection
        {
            public string ProjectedProperty { get; set; }
            public string TheChildNestedProperty { get; set; }
        }

        public class EntityIndex
        {
            public string Property { get; set; }
            public int? YksiKaksiKolme { get; set; }
        }

        public class EntityWithoutId
        {
            public string Data { get; set; }
        }

        public class AProjection
        {
            public int Number { get; set; }
            public int Property { get; set; }
        }
    }
}