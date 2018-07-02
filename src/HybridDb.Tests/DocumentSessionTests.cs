using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FakeItEasy;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentSessionTests : HybridDbAutoInitializeTests
    {
        [Fact]
        public void CanEvictEntity()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity1 = new Entity { Id = id, Property = "Asger" };
                session.Store(entity1);
                session.Advanced.IsLoaded<Entity>(id).ShouldBe(true);
                session.Advanced.Evict(entity1);
                session.Advanced.IsLoaded<Entity>(id).ShouldBe(false);
            }
        }

        [Fact]
        public async Task CanGetEtagForEntity()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                var entity1 = new Entity { Id = NewId(), Property = "Asger" };
                session.Advanced.GetEtagFor(entity1).ShouldBe(null);

                session.Store(entity1);
                session.Advanced.GetEtagFor(entity1).ShouldBe(Guid.Empty);

                await session.SaveChanges();
                session.Advanced.GetEtagFor(entity1).ShouldNotBe(null);
                session.Advanced.GetEtagFor(entity1).ShouldNotBe(Guid.Empty);
            }
        }

        [Fact]
        public async Task CanDeferCommands()
        {
            Document<Entity>();

            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            using (var session = store.OpenSession())
            {
                // the initial migrations might issue some requests
                var initialNumberOfRequest = store.NumberOfRequests;

                session.Advanced.Defer(new InsertCommand(table.Table, id, new { }));
                store.NumberOfRequests.ShouldBe(initialNumberOfRequest + 0);
                await session.SaveChanges();
                store.NumberOfRequests.ShouldBe(initialNumberOfRequest + 1);
            }

            var entity = store.Database.RawQuery<dynamic>($"select * from #Entities where Id = '{id}'").SingleOrDefault();
            Assert.NotNull(entity);
        }

        [Fact]
        public void CanOpenSession()
        {
            store.OpenSession().ShouldNotBe(null);
        }

        [Fact]
        public async Task CanStoreDocument()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity());
                await session.SaveChanges();
            }

            var entity = store.Database.RawQuery<dynamic>("select * from #Entities").SingleOrDefault();
            Assert.NotNull(entity);
            Assert.NotNull(entity.Document);
            Assert.NotEqual(0, entity.Document.Length);
        }

        [Fact]
        public async Task AccessToNestedPropertyCanBeNullSafe()
        {
            Document<Entity>().With(x => x.TheChild.NestedProperty);

            using (var session = store.OpenSession())
            {
                session.Store(new Entity
                {
                    TheChild = null
                });
                await session.SaveChanges();
            }

            var entity = store.Database.RawQuery<dynamic>("select * from #Entities").SingleOrDefault();
            Assert.Null(entity.TheChildNestedProperty);
        }

        [Fact]
        public void AccessToNestedPropertyThrowsIfNotMadeNullSafe()
        {
            Document<Entity>().With(x => x.TheChild.NestedProperty, new DisableNullCheckInjection());

            using (var session = store.OpenSession())
            {
                session.Store(new Entity
                {
                    TheChild = null
                });
                Should.Throw<TargetInvocationException>(() => session.SaveChanges());
            }
        }

        [Fact]
        public async Task StoresIdRetrievedFromObject()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                await session.SaveChanges();
            }

            var retrivedId = store.Database.RawQuery<string>("select Id from #Entities").SingleOrDefault();
            retrivedId.ShouldBe(id);
        }

        [Fact]
        public async Task StoringDocumentWithSameIdTwiceIsIgnored()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity1 = new Entity { Id = id, Property = "Asger" };
                var entity2 = new Entity { Id = id, Property = "Asger" };
                session.Store(entity1);
                session.Store(entity2);
                (await session.Load<Entity>(id)).ShouldBe(entity1);
            }
        }

        [Fact]
        public async Task SessionWillNotSaveWhenSaveChangesIsNotCalled()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id, Property = "Asger" };
                session.Store(entity);
                await session.SaveChanges();
                entity.Property = "Lars";
                session.Advanced.Clear();
                (await session.Load<Entity>(id)).Property.ShouldBe("Asger");
            }
        }

        [Fact]
        public async Task CanSaveChangesWithPessimisticConcurrency()
        {
            Document<Entity>();

            var id = NewId();
            using (var session1 = store.OpenSession())
            using (var session2 = store.OpenSession())
            {
                var entityFromSession1 = new Entity { Id = id, Property = "Asger" };
                session1.Store(entityFromSession1);
                await session1.SaveChanges();

                var entityFromSession2 = await session2.Load<Entity>(id);
                entityFromSession2.Property = " er craazy";
                await session2.SaveChanges();

                entityFromSession1.Property += " er 4 real";
                await session1.SaveChanges(lastWriteWins: true, forceWriteUnchangedDocument: false);

                session1.Advanced.Clear();
                (await session1.Load<Entity>(id)).Property.ShouldBe("Asger er 4 real");
            }
        }

        [Fact]
        public async Task CanDeleteDocument()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Load<Entity>(id);
                session.Delete(entity);
                await session.SaveChanges();
                session.Advanced.Clear();

                session.Load<Entity>(id).ShouldBe(null);
            }
        }

        [Fact]
        public void DeletingATransientEntityRemovesItFromSession()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id, Property = "Asger" };
                session.Store(entity);
                session.Delete(entity);
                session.Advanced.IsLoaded<Entity>(id).ShouldBe(false);
            }
        }

        [Fact]
        public void DeletingAnEntityNotLoadedInSessionIsIgnored()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id, Property = "Asger" };
                Should.NotThrow(() => session.Delete(entity));
                session.Advanced.IsLoaded<Entity>(id).ShouldBe(false);
            }
        }

        [Fact]
        public async Task LoadingADocumentMarkedForDeletionReturnNothing()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entity = await session.Load<Entity>(id);
                session.Delete(entity);
                session.Load<Entity>(id).ShouldBe(null);
            }
        }

        [Fact]
        public async Task LoadingAManagedDocumentWithWrongTypeReturnsNothing()
        {
            Document<Entity>();
            Document<OtherEntity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                var otherEntity = await session.Load<OtherEntity>(id);
                otherEntity.ShouldBe(null);
            }
        }

        [Fact]
        public void CanClearSession()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.Advanced.Clear();
                session.Advanced.IsLoaded<Entity>(id).ShouldBe(false);
            }
        }

        [Fact]
        public void CanCheckIfEntityIsLoadedInSession()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Advanced.IsLoaded<Entity>(id).ShouldBe(false);
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.Advanced.IsLoaded<Entity>(id).ShouldBe(true);
            }
        }

        [Fact]
        public async Task CanLoadDocument()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entity = await session.Load<Entity>(id);
                entity.Property.ShouldBe("Asger");
            }
        }

        [Fact]
        public async Task CanLoadDocumentWithDesign()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entity = await session.Load(typeof(Entity), id);

                entity.ShouldBeOfType<Entity>();
            }
        }

        [Fact]
        public async Task LoadingSameDocumentTwiceInSameSessionReturnsSameInstance()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var document1 = await session.Load<Entity>(id);
                var document2 = await session.Load<Entity>(id);
                document1.ShouldBe(document2);
            }
        }

        [Fact]
        public async Task CanLoadATransientEntity()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id, Property = "Asger" };
                session.Store(entity);
                (await session.Load<Entity>(id)).ShouldBe(entity);
            }
        }

        [Fact]
        public async Task LoadingANonExistingDocumentReturnsNull()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                (await session.Load<Entity>(NewId())).ShouldBe(null);
            }
        }

        [Fact]
        public async Task SavesChangesWhenObjectHasChanged()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                // the initial migrations might issue some requests
                var initialNumberOfRequest = store.NumberOfRequests;

                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges(); // 1
                session.Advanced.Clear();

                var document = await session.Load<Entity>(id); // 2
                document.Property = "Lars";
                await session.SaveChanges(); // 3

                store.NumberOfRequests.ShouldBe(initialNumberOfRequest + 3);
                session.Advanced.Clear();
                (await session.Load<Entity>(id)).Property.ShouldBe("Lars");
            }
        }

        [Fact]
        public async Task DoesNotSaveChangesWhenObjectHasNotChanged()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                // the initial migrations might issue some requests
                var initialNumberOfRequest = store.NumberOfRequests;

                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges(); // 1
                session.Advanced.Clear();

                await session.Load<Entity>(id); // 2
                await session.SaveChanges(); // Should not issue a request

                store.NumberOfRequests.ShouldBe(initialNumberOfRequest + 2);
            }
        }

        //[Fact]
        //public async Task BatchesMultipleChanges()
        //{
        //    Document<Entity>();

        //    var id1 = NewId();
        //    var id2 = NewId();
        //    var id3 = NewId();

        //    using (var session = store.OpenSession())
        //    {
        //        // the initial migrations might issue some requests
        //        var initialNumberOfRequest = store.NumberOfRequests;

        //        session.Store(new Entity { Id = id1, Property = "Asger" });
        //        session.Store(new Entity { Id = id2, Property = "Asger" });
        //        await session.SaveChanges(); // 1
        //        store.NumberOfRequests.ShouldBe(initialNumberOfRequest + 1);
        //        session.Advanced.Clear();

        //        var entity1 = await session.Load<Entity>(id1); // 2
        //        var entity2 = await session.Load<Entity>(id2); // 3
        //        store.NumberOfRequests.ShouldBe(initialNumberOfRequest + 3);

        //        session.Delete(entity1);
        //        entity2.Property = "Lars";
        //        session.Store(new Entity { Id = id3, Property = "Jacob" });
        //        await session.SaveChanges(); // 4
        //        store.NumberOfRequests.ShouldBe(initialNumberOfRequest + 4);
        //    }
        //}

        [Fact]
        public async Task SavingChangesTwiceInARowDoesNothing()
        {
            Document<Entity>();

            var id1 = NewId();
            var id2 = NewId();
            var id3 = NewId();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id1, Property = "Asger" });
                session.Store(new Entity { Id = id2, Property = "Asger" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entity1 = await session.Load<Entity>(id1);
                var entity2 = await session.Load<Entity>(id2);
                session.Delete(entity1);
                entity2.Property = "Lars";
                session.Store(new Entity { Id = id3, Property = "Jacob" });
                await session.SaveChanges();

                var numberOfRequests = store.NumberOfRequests;
                var lastEtag = store.LastWrittenEtag;

                await session.SaveChanges();

                store.NumberOfRequests.ShouldBe(numberOfRequests);
                store.LastWrittenEtag.ShouldBe(lastEtag);
            }
        }

        [Fact]
        public async Task IssuesConcurrencyExceptionWhenTwoSessionsChangeSameDocument()
        {
            Document<Entity>();

            var id1 = NewId();

            using (var session1 = store.OpenSession())
            using (var session2 = store.OpenSession())
            {
                session1.Store(new Entity { Id = id1, Property = "Asger" });
                await session1.SaveChanges();

                var entity1 = await session1.Load<Entity>(id1);
                var entity2 = await session2.Load<Entity>(id1);

                entity1.Property = "A";
                await session1.SaveChanges();

                entity2.Property = "B";
                Should.Throw<ConcurrencyException>(() => session2.SaveChanges());
            }
        }

        [Fact]
        public async Task CanQueryDocument()
        {
            Document<Entity>().With(x => x.ProjectedProperty);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large" });
                session.Store(new Entity { Id = id, Property = "Lars", ProjectedProperty = "Small" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<Entity>().Where(x => x.ProjectedProperty == "Large").ToList();

                entities.Count.ShouldBe(1);
                entities[0].Property.ShouldBe("Asger");
                entities[0].ProjectedProperty.ShouldBe("Large");
            }
        }

        [Fact]
        public async Task CanSaveChangesOnAQueriedDocument()
        {
            Document<Entity>().With(x => x.ProjectedProperty);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Query<Entity>().Single(x => x.ProjectedProperty == "Large");

                entity.Property = "Lars";
                await session.SaveChanges();
                session.Advanced.Clear();

                (await session.Load<Entity>(id)).Property.ShouldBe("Lars");
            }
        }

        [Fact]
        public async Task QueryingALoadedDocumentReturnsSameInstance()
        {
            Document<Entity>().With(x => x.ProjectedProperty);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var instance1 = await session.Load<Entity>(id);
                var instance2 = session.Query<Entity>().Single(x => x.ProjectedProperty == "Large");

                instance1.ShouldBe(instance2);
            }
        }

        [Fact]
        public async Task QueryingALoadedDocumentMarkedForDeletionReturnsNothing()
        {
            Document<Entity>().With(x => x.ProjectedProperty);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entity = await session.Load<Entity>(id);
                session.Delete(entity);

                var entities = session.Query<Entity>().Where(x => x.ProjectedProperty == "Large").ToList();

                entities.Count.ShouldBe(0);
            }
        }

        [Fact]
        public async Task CanQueryAndReturnProjection()
        {
            Document<Entity>()
                .With(x => x.ProjectedProperty)
                .With(x => x.TheChild.NestedProperty);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large", TheChild = new Entity.Child { NestedProperty = "Hans" } });
                session.Store(new Entity { Id = id, Property = "Lars", ProjectedProperty = "Small", TheChild = new Entity.Child { NestedProperty = "Peter" } });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<Entity>().Where(x => x.ProjectedProperty == "Large").AsProjection<EntityProjection>().ToList();

                entities.Count.ShouldBe(1);
                entities[0].ProjectedProperty.ShouldBe("Large");
                entities[0].TheChildNestedProperty.ShouldBe("Hans");
            }
        }

        [Fact]
        public async Task WillNotSaveChangesToProjectedValues()
        {
            Document<Entity>()
                .With(x => x.ProjectedProperty)
                .With(x => x.TheChild.NestedProperty);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large" });
                await session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Query<Entity>().AsProjection<EntityProjection>().Single(x => x.ProjectedProperty == "Large");
                entity.ProjectedProperty = "Small";
                await session.SaveChanges();
                session.Advanced.Clear();

                (await session.Load<Entity>(id)).ProjectedProperty.ShouldBe("Large");
            }
        }


        [Fact]
        public async Task StoreOneTypeAndLoadAnotherFromSameTableAndKeyResultsInOneManagedEntity()
        {
            using (var session = store.OpenSession())
            {
                session.Store("key", new OtherEntity());
                await session.SaveChanges();

                (await session.Load<object>("key")).ShouldNotBe(null);

                session.Advanced.ManagedEntities.Count().ShouldBe(1);
            }
        }

        [Fact]
        public void StoreTwoInstancesToSameTableWithSameIdIsIgnored()
        {
            // this may not be desired behavior, but it's been this way for a while

            Document<DerivedEntity>();
            Document<MoreDerivedEntity1>();

            using (var session = store.OpenSession())
            {
                session.Store("key", new MoreDerivedEntity1());
                session.Store("key", new DerivedEntity());

                session.Advanced.ManagedEntities.Count().ShouldBe(1);
            }
        }

        [Fact]
        public async Task CanQueryUserDefinedProjection()
        {
            Document<OtherEntity>().With("Unknown", x => 2);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new OtherEntity { Id = id });
                await session.SaveChanges();

                var entities = session.Query<OtherEntity>().Where(x => x.Column<int>("Unknown") == 2).ToList();
                entities.Count.ShouldBe(1);
            }
        }

        [Fact]
        public async Task Bug10_UsesProjectedTypeToSelectColumns()
        {
            Document<Entity>()
                .With(x => x.Property)
                .With(x => x.Number);

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "asger", Number = 2 });
                await session.SaveChanges();

                Should.NotThrow(() => session.Query<Entity>().Select(x => new AProjection { Number = x.Number }).ToList());
            }
        }


        [Fact]
        public async Task CanQueryIndex()
        {
            Document<AbstractEntity>()
                .Extend<EntityIndex>(e => e.With(x => x.YksiKaksiKolme, x => x.Number))
                .With(x => x.Property);
            Document<MoreDerivedEntity1>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id, Property = "Asger", Number = 2 });
                await session.SaveChanges();

                //var test = from x in session.Query<AbstractEntity>()
                //           let index = x.Index<EntityIndex>()
                //           where x.Property == "Asger" && index.YksiKaksiKolme > 1
                //           select x;

                var entity = session.Query<AbstractEntity>().Single(x => x.Property == "Asger" && x.Index<EntityIndex>().YksiKaksiKolme > 1);
                entity.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public async Task AppliesMigrationsOnLoad()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges();
            }

            Reset();

            Document<Entity>();
            DisableDocumentMigrationsInBackground();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] = "Peter"; })));

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>(id);
                entity.Property.ShouldBe("Peter");
            }
        }

        [Fact]
        public async Task AppliesMigrationsOnQuery()
        {
            Document<AbstractEntity>().With(x => x.Number);
            Document<DerivedEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Property = "Asger", Number = 1 });
                session.Store(new MoreDerivedEntity1 { Property = "Jacob", Number = 2 });
                session.Store(new MoreDerivedEntity2 { Property = "Lars", Number = 3 });
                await session.SaveChanges();
            }

            Reset();

            Document<AbstractEntity>().With(x => x.Number);
            Document<DerivedEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            UseMigrations(
                new InlineMigration(1, new ChangeDocumentAsJObject<AbstractEntity>(x => { x["Property"] = x["Property"] + " er cool"; })),
                new InlineMigration(2, new ChangeDocumentAsJObject<MoreDerivedEntity2>(x => { x["Property"] = x["Property"] + "io"; })));

            using (var session = store.OpenSession())
            {
                var rows = session.Query<AbstractEntity>().OrderBy(x => x.Number).ToList();

                rows[0].Property.ShouldBe("Asger er cool");
                rows[1].Property.ShouldBe("Jacob er cool");
                rows[2].Property.ShouldBe("Lars er coolio");
            }
        }

        [Fact]
        public async Task TracksChangesFromMigrations()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges();
            }

            Reset();

            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] = "Peter"; })));

            var numberOfRequests = store.NumberOfRequests;
            using (var session = store.OpenSession())
            {
                await session.Load<Entity>(id);
                await session.SaveChanges();
            }

            (store.NumberOfRequests - numberOfRequests).ShouldBe(1);
        }

        [Fact]
        public async Task AddsVersionOnInsert()
        {
            Document<Entity>();
            UseMigrations(new InlineMigration(1), new InlineMigration(2));

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                await session.SaveChanges();
            }

            var table = configuration.GetDesignFor<Entity>().Table;
            var row = await store.Get(table, id);
            ((int)row[table.VersionColumn]).ShouldBe(2);
        }

        [Fact]
        public async Task UpdatesVersionOnUpdate()
        {
            Document<Entity>();

            var id = NewId();
            var table = configuration.GetDesignFor<Entity>().Table;
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                await session.SaveChanges();
            }

            Reset();
            Document<Entity>();
            InitializeStore();
            UseMigrations(new InlineMigration(1), new InlineMigration(2));

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>(id);
                entity.Number++;
                await session.SaveChanges();
            }

            var row = await store.Get(table, id);
            ((int)row[table.VersionColumn]).ShouldBe(2);
        }

        [Fact]
        public async Task BacksUpMigratedDocumentOnSave()
        {
            var id = NewId();

            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges();
            }

            var backupWriter = new FakeBackupWriter();

            Reset();

            UseBackupWriter(backupWriter);
            Document<Entity>();
            DisableDocumentMigrationsInBackground();
            UseMigrations(
                new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "1"; })));

            using (var session = store.OpenSession())
            {
                await session.Load<Entity>(id);

                // no backup on load
                backupWriter.Files.Count.ShouldBe(0);

                await session.SaveChanges();

                // backup on save
                backupWriter.Files.Count.ShouldBe(1);
                backupWriter.Files[$"HybridDb.Tests.HybridDbTests+Entity_{id}_0.bak"]
                    .ShouldBe(configuration.Serializer.Serialize(new Entity { Id = id, Property = "Asger" }));
            }

            // try it again (todo: move to seperate test)
            Reset();

            UseBackupWriter(backupWriter);
            Document<Entity>();
            DisableDocumentMigrationsInBackground();
            UseMigrations(
                new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "1"; })),
                new InlineMigration(2, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "2"; })));

            using (var session = store.OpenSession())
            {
                await session.Load<Entity>(id);

                // no backup on load
                backupWriter.Files.Count.ShouldBe(1);

                await session.SaveChanges();

                // backup on save
                backupWriter.Files.Count.ShouldBe(2);
                backupWriter.Files[$"HybridDb.Tests.HybridDbTests+Entity_{id}_1.bak"]
                    .ShouldBe(configuration.Serializer.Serialize(new Entity { Id = id, Property = "Asger1" }));
            }
        }

        [Fact]
        public async Task DoesNotBackUpDocumentWithNoMigrations()
        {
            Document<Entity>();

            var backupWriter = new FakeBackupWriter();
            UseBackupWriter(backupWriter);
            DisableDocumentMigrationsInBackground();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>(id);

                entity.Property = "Jacob";

                await session.SaveChanges();

                // no migrations, means no backup
                backupWriter.Files.Count.ShouldBe(0);
            }
        }

        [Fact]
        public async Task FailsOnSaveChangesWhenPreviousSaveFailed()
        {
            var fakeStore = A.Fake<IDocumentStore>(x => x.Wrapping(store));

            A.CallTo(fakeStore)
                .Where(x => x.Method.Name == nameof(IDocumentStore.Execute))
                .Throws(() => new Exception());

            Document<Entity>();

            // The this reference inside OpenSession() is not referencing the fake store, but the wrapped store itself.
            // Therefore we bypass the OpenSession factory.
            using (var session = new DocumentSession(fakeStore))
            {
                session.Store(new Entity());

                try
                {
                    await session.SaveChanges(); // fails when in an inconsitent state
                }
                catch (Exception) { }

                Should.Throw<InvalidOperationException>(() => session.SaveChanges())
                    .Message.ShouldBe("Session is not in a valid state. Please dispose it and open a new one.");
            }
        }

        [Fact]
        public async Task DoesNotFailsOnSaveChangesWhenPreviousSaveWasSuccessful()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity());

                await session.SaveChanges();
                await session.SaveChanges();
            }
        }

        [Fact]
        public async Task DoesNotFailsOnSaveChangesWhenPreviousSaveWasNoop()
        {
            using (var session = store.OpenSession())
            {
                await session.SaveChanges();
                await session.SaveChanges();
            }
        }

        [Fact]
        public async Task CanUseADifferentIdProjection()
        {
            Document<Entity>().Key(x => x.Property);

            using (var session = store.OpenSession())
            {
                session.Store(new Entity() { Property = "TheId" });
                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>("TheId");

                entity.ShouldNotBe(null);
            }
        }

        [Fact]
        public async Task CanStoreWithGivenKey()
        {
            Document<EntityWithoutId>();

            using (var session = store.OpenSession())
            {
                session.Store("mykey", new EntityWithoutId { Data = "TheId" });
                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<EntityWithoutId>("mykey");

                entity.ShouldNotBe(null);
            }
        }

        [Fact]
        public async Task CanStoreMetadata()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id };
                session.Store(entity);
                session.Advanced.SetMetadataFor(entity, new Dictionary<string, List<string>>
                {
                    ["key"] = new List<string> { "value1", "value2" }
                });

                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>(id);

                var metadata = session.Advanced.GetMetadataFor(entity);

                metadata["key"].ShouldBe(new List<string> { "value1", "value2" });
            }
        }

        [Fact]
        public async Task CanStoreWithoutOverwritingMetadata()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id };
                session.Store(entity);
                session.Advanced.SetMetadataFor(entity, new Dictionary<string, List<string>>
                {
                    ["key"] = new List<string> { "value1", "value2" }
                });

                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>(id);
                entity.Field = "a new field";
                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>(id);

                var metadata = session.Advanced.GetMetadataFor(entity);

                metadata["key"].ShouldBe(new List<string> { "value1", "value2" });
            }
        }

        [Fact]
        public async Task CanUpdateMetadata()
        {
            Document<Entity>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id };
                session.Store(entity);
                session.Advanced.SetMetadataFor(entity, new Dictionary<string, List<string>>
                {
                    ["key"] = new List<string> { "value1", "value2" }
                });

                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>(id);
                session.Advanced.SetMetadataFor(entity, new Dictionary<string, List<string>>
                {
                    ["another-key"] = new List<string> { "value" }
                });
                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>(id);

                var metadata = session.Advanced.GetMetadataFor(entity);

                metadata.Keys.Count.ShouldBe(1);
                metadata["another-key"].ShouldBe(new List<string> { "value" });
            }
        }

        [Fact]
        public async Task CanSetMetadataToNull()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                var entity = new Entity();
                session.Store("id", entity);
                session.Advanced.SetMetadataFor(entity, new Dictionary<string, List<string>>
                {
                    ["key"] = new List<string> { "value1", "value2" }
                });

                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>("id");
                session.Advanced.SetMetadataFor(entity, null);
                await session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = await session.Load<Entity>("id");

                var metadata = session.Advanced.GetMetadataFor(entity);

                metadata.ShouldBe(null);
            }
        }


        [Fact]
        public async Task UsesIdToStringAsDefaultKeyResolver()
        {
            using (var session = store.OpenSession())
            {
                var id = Guid.NewGuid();
                session.Store(new EntityWithFunnyKey
                {
                    Id = id
                });

                await session.SaveChanges();
                session.Advanced.Clear();

                (await session.Load<EntityWithFunnyKey>(id.ToString())).Id.ShouldBe(id);
            }
        }

        [Fact]
        public async Task CanSetDefaultKeyResolver()
        {
            UseKeyResolver(x => "asger");

            using (var session = store.OpenSession())
            {
                session.Store(new object());

                await session.SaveChanges();
                session.Advanced.Clear();

                (await session.Load<object>("asger")).ShouldNotBe(null);
            }
        }

        [Fact]
        public void FailsIfStringProjectionIsTruncated()
        {
            Document<Entity>().With(x => x.Field, new MaxLength(10));

            using (var session = store.OpenSession())
            {
                session.Store(new Entity
                {
                    Field = "1234567890+"
                });

                Should.Throw<SqlException>(() => session.SaveChanges());
            }
        }

        [Fact(Skip = "Feature on holds")]
        public void CanProjectCollection()
        {
            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity1 = new Entity
                {
                    Id = id,
                    Children =
                    {
                        new Entity.Child {NestedProperty = "A"},
                        new Entity.Child {NestedProperty = "B"}
                    }
                };

                session.Store(entity1);
            }
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