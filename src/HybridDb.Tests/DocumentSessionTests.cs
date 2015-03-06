using System;
using System.Collections.Generic;
using System.Reflection;
using HybridDb.Commands;
using Shouldly;
using Xunit;
using System.Linq;
using HybridDb.Linq;

namespace HybridDb.Tests
{
    public class DocumentSessionTests : HybridDbTests
    {
        [Fact(Skip = "Feature on holds")]
        public void CanProjectCollection()
        {
            var id = Guid.NewGuid();
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

        [Fact]
        public void CanEvictEntity()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                var entity1 = new Entity { Id = id, Property = "Asger" };
                session.Store(entity1);
                session.Advanced.IsLoaded(id).ShouldBe(true);
                session.Advanced.Evict(entity1);
                session.Advanced.IsLoaded(id).ShouldBe(false);
            }
        }

        [Fact]
        public void CanGetEtagForEntity()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                var entity1 = new Entity { Id = Guid.NewGuid(), Property = "Asger" };
                session.Advanced.GetEtagFor(entity1).ShouldBe(null);

                session.Store(entity1);
                session.Advanced.GetEtagFor(entity1).ShouldBe(Guid.Empty);
                
                session.SaveChanges();
                session.Advanced.GetEtagFor(entity1).ShouldNotBe(null);
                session.Advanced.GetEtagFor(entity1).ShouldNotBe(Guid.Empty);
            }
        }

        [Fact]
        public void CanDeferCommands()
        {
            Document<Entity>();

            var table = store.Configuration.GetDesignFor<Entity>();
            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Advanced.Defer(new InsertCommand(table.Table, id, new{}));
                store.NumberOfRequests.ShouldBe(0);
                session.SaveChanges();
                store.NumberOfRequests.ShouldBe(1);
            }

            var entity = store.RawQuery<dynamic>(string.Format("select * from #Entities where Id = '{0}'", id)).SingleOrDefault();
            Assert.NotNull(entity);
        }

        [Fact]
        public void CanOpenSession()
        {
            store.OpenSession().ShouldNotBe(null);
        }

        [Fact]
        public void CanStoreDocument()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity());
                session.SaveChanges();
            }

            var entity = store.RawQuery<dynamic>("select * from #Entities").SingleOrDefault();
            Assert.NotNull(entity);
            Assert.NotNull(entity.Document);
            Assert.NotEqual(0, entity.Document.Length);
        }

        [Fact]
        public void AccessToNestedPropertyCanBeNullSafe()
        {
            Document<Entity>().With(x => x.TheChild.NestedProperty);

            using (var session = store.OpenSession())
            {
                session.Store(new Entity
                {
                    TheChild = null
                });
                session.SaveChanges();
            }

            var entity = store.RawQuery<dynamic>("select * from #Entities").SingleOrDefault();
            Assert.Null(entity.TheChildNestedProperty);
        }

        [Fact]
        public void AccessToNestedPropertyThrowsIfNotMadeNullSafe()
        {
            Document<Entity>().With(x => x.TheChild.NestedProperty, makeNullSafe: false);

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
        public void StoresIdRetrievedFromObject()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id });
                session.SaveChanges();
            }

            var retrivedId = store.RawQuery<Guid>("select Id from #Entities").SingleOrDefault();
            retrivedId.ShouldBe(id);
        }

        [Fact]
        public void StoringDocumentWithSameIdTwiceIsIgnored()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                var entity1 = new Entity {Id = id, Property = "Asger"};
                var entity2 = new Entity {Id = id, Property = "Asger"};
                session.Store(entity1);
                session.Store(entity2);
                session.Load<Entity>(id).ShouldBe(entity1);
            }
        }

        [Fact]
        public void SessionWillNotSaveWhenSaveChangesIsNotCalled()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                var entity = new Entity {Id = id, Property = "Asger"};
                session.Store(entity);
                session.SaveChanges();
                entity.Property = "Lars";
                session.Advanced.Clear();
                session.Load<Entity>(id).Property.ShouldBe("Asger");
            }
        }

        [Fact]
        public void CanSaveChangesWithPessimisticConcurrency()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session1 = store.OpenSession())
            using (var session2 = store.OpenSession())
            {
                var entityFromSession1 = new Entity { Id = id, Property = "Asger" };
                session1.Store(entityFromSession1);
                session1.SaveChanges();

                var entityFromSession2 = session2.Load<Entity>(id);
                entityFromSession2.Property = " er craazy";
                session2.SaveChanges();

                entityFromSession1.Property += " er 4 real";
                session1.Advanced.SaveChangesLastWriterWins();

                session1.Advanced.Clear();
                session1.Load<Entity>(id).Property.ShouldBe("Asger er 4 real");
            }
        }

        [Fact]
        public void CanDeleteDocument()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Load<Entity>(id);
                session.Delete(entity);
                session.SaveChanges();
                session.Advanced.Clear();

                session.Load<Entity>(id).ShouldBe(null);
            }
        }

        [Fact]
        public void DeletingATransientEntityRemovesItFromSession()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                var entity = new Entity {Id = id, Property = "Asger"};
                session.Store(entity);
                session.Delete(entity);
                session.Advanced.IsLoaded(id).ShouldBe(false);
            }
        }

        [Fact]
        public void DeletingAnEntityNotLoadedInSessionIsIgnored()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = id, Property = "Asger" };
                Should.NotThrow(() => session.Delete(entity));
                session.Advanced.IsLoaded(id).ShouldBe(false);
            }
        }

        [Fact]
        public void LoadingADocumentMarkedForDeletionReturnNothing()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();
                
                var entity = session.Load<Entity>(id);
                session.Delete(entity);
                session.Load<Entity>(id).ShouldBe(null);
            }
        }

        [Fact]
        public void LoadingAManagedDocumentWithWrongTypeReturnsNothing()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                var otherEntity = session.Load<OtherEntity>(id);
                otherEntity.ShouldBe(null);
            }
        }

        [Fact]
        public void CanClearSession()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.Advanced.Clear();
                session.Advanced.IsLoaded(id).ShouldBe(false);
            }
        }

        [Fact]
        public void CanCheckIfEntityIsLoadedInSession()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Advanced.IsLoaded(id).ShouldBe(false);
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.Advanced.IsLoaded(id).ShouldBe(true);
            }
        }

        [Fact]
        public void CanLoadDocument()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Load<Entity>(id);
                entity.Property.ShouldBe("Asger");
            }
        }

        [Fact]
        public void LoadingSameDocumentTwiceInSameSessionReturnsSameInstance()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var document1 = session.Load<Entity>(id);
                var document2 = session.Load<Entity>(id);
                document1.ShouldBe(document2);
            }
        }

        [Fact]
        public void CanLoadATransientEntity()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                var entity = new Entity {Id = id, Property = "Asger"};
                session.Store(entity);
                session.Load<Entity>(id).ShouldBe(entity);
            }
        }

        [Fact]
        public void LoadingANonExistingDocumentReturnsNull()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Load<Entity>(Guid.NewGuid()).ShouldBe(null);
            }
        }

        [Fact]
        public void SavesChangesWhenObjectHasChanged()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges(); // 1
                session.Advanced.Clear();

                var document = session.Load<Entity>(id); // 2
                document.Property = "Lars";
                session.SaveChanges(); // 3

                store.NumberOfRequests.ShouldBe(3);
                session.Advanced.Clear();
                session.Load<Entity>(id).Property.ShouldBe("Lars");
            }
        }

        [Fact]
        public void DoesNotSaveChangesWhenObjectHasNotChanged()
        {
            Document<Entity>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger" });
                session.SaveChanges(); // 1
                session.Advanced.Clear();

                session.Load<Entity>(id); // 2
                session.SaveChanges(); // Should not issue a request

                store.NumberOfRequests.ShouldBe(2);
            }
        }

        [Fact]
        public void BatchesMultipleChanges()
        {
            Document<Entity>();

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id1, Property = "Asger" });
                session.Store(new Entity { Id = id2, Property = "Asger" });
                session.SaveChanges(); // 1
                store.NumberOfRequests.ShouldBe(1);
                session.Advanced.Clear();

                var entity1 = session.Load<Entity>(id1); // 2
                var entity2 = session.Load<Entity>(id2); // 3
                store.NumberOfRequests.ShouldBe(3);

                session.Delete(entity1);
                entity2.Property = "Lars";
                session.Store(new Entity { Id = id3, Property = "Jacob" });
                session.SaveChanges(); // 4
                store.NumberOfRequests.ShouldBe(4);
            }
        }

        [Fact]
        public void SavingChangesTwiceInARowDoesNothing()
        {
            Document<Entity>();

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using (var session = store.OpenSession())
            {
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

                var numberOfRequests = store.NumberOfRequests;
                var lastEtag = store.LastWrittenEtag;

                session.SaveChanges();

                store.NumberOfRequests.ShouldBe(numberOfRequests);
                store.LastWrittenEtag.ShouldBe(lastEtag);
            }
        }

        [Fact]
        public void IssuesConcurrencyExceptionWhenTwoSessionsChangeSameDocument()
        {
            Document<Entity>();

            var id1 = Guid.NewGuid();

            using (var session1 = store.OpenSession())
            using (var session2 = store.OpenSession())
            {
                session1.Store(new Entity { Id = id1, Property = "Asger" });
                session1.SaveChanges();

                var entity1 = session1.Load<Entity>(id1);
                var entity2 = session2.Load<Entity>(id1);

                entity1.Property = "A";
                session1.SaveChanges();

                entity2.Property = "B";
                Should.Throw<ConcurrencyException>(() => session2.SaveChanges());
            }
        }

        [Fact]
        public void CanQueryDocument()
        {
            Document<Entity>().With(x => x.ProjectedProperty);

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large"});
                session.Store(new Entity { Id = id, Property = "Lars", ProjectedProperty = "Small" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<Entity>().Where(x => x.ProjectedProperty == "Large").ToList();
                
                entities.Count.ShouldBe(1);
                entities[0].Property.ShouldBe("Asger");
                entities[0].ProjectedProperty.ShouldBe("Large");
            }
        }

        [Fact]
        public void CanSaveChangesOnAQueriedDocument()
        {
            Document<Entity>().With(x => x.ProjectedProperty);

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large"});
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Query<Entity>().Single(x => x.ProjectedProperty == "Large");

                entity.Property = "Lars";
                session.SaveChanges();
                session.Advanced.Clear();

                session.Load<Entity>(id).Property.ShouldBe("Lars");
            }
        }

        [Fact]
        public void QueryingALoadedDocumentReturnsSameInstance()
        {
            Document<Entity>().With(x => x.ProjectedProperty);

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large"});
                session.SaveChanges();
                session.Advanced.Clear();

                var instance1 = session.Load<Entity>(id);
                var instance2 = session.Query<Entity>().Single(x => x.ProjectedProperty == "Large");

                instance1.ShouldBe(instance2);
            }
        }

        [Fact]
        public void QueryingALoadedDocumentMarkedForDeletionReturnsNothing()
        {
            Document<Entity>().With(x => x.ProjectedProperty);

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large"});
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Load<Entity>(id);
                session.Delete(entity);

                var entities = session.Query<Entity>().Where(x => x.ProjectedProperty == "Large").ToList();

                entities.Count.ShouldBe(0);
            }
        }

        [Fact]
        public void CanQueryAndReturnProjection()
        {
            Document<Entity>()
                .With(x => x.ProjectedProperty)
                .With(x => x.TheChild.NestedProperty);

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id, Property = "Asger", ProjectedProperty = "Large", TheChild = new Entity.Child { NestedProperty = "Hans" } });
                session.Store(new Entity { Id = id, Property = "Lars", ProjectedProperty = "Small", TheChild = new Entity.Child { NestedProperty = "Peter" } });
                session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<Entity>().Where(x => x.ProjectedProperty == "Large").AsProjection<EntityProjection>().ToList();

                entities.Count.ShouldBe(1);
                entities[0].ProjectedProperty.ShouldBe("Large");
                entities[0].TheChildNestedProperty.ShouldBe("Hans");
            }
        }

        [Fact]
        public void WillNotSaveChangesToProjectedValues()
        {
            Document<Entity>()
                .With(x => x.ProjectedProperty)
                .With(x => x.TheChild.NestedProperty);

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new Entity {Id = id, Property = "Asger", ProjectedProperty = "Large"});
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Query<Entity>().AsProjection<EntityProjection>().Single(x => x.ProjectedProperty == "Large");
                entity.ProjectedProperty = "Small";
                session.SaveChanges();
                session.Advanced.Clear();

                session.Load<Entity>(id).ProjectedProperty.ShouldBe("Large");
            }
        }

        [Fact]
        public void CanLoadByInterface()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id, Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entity1 = session.Load<ISomeInterface>(id);
                entity1.ShouldBeOfType<MoreDerivedEntity1>();
                entity1.Property.ShouldBe("Asger");

                var entity2 = session.Load<IOtherInterface>(id);
                entity2.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void CanLoadDerivedEntityByBasetype()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id });
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Load<AbstractEntity>(id);
                entity.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void CanLoadDerivedEntityByOwnType()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id });
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Load<MoreDerivedEntity1>(id);
                entity.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void LoadingDerivedEntityBySiblingTypeThrows()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id });
                session.SaveChanges();
                session.Advanced.Clear();

                Should.Throw<InvalidOperationException>(() => session.Load<MoreDerivedEntity2>(id))
                    .Message.ShouldBe("Document with id " + id + " exists, but is not assignable to the given type MoreDerivedEntity2.");
            }
        }

        [Fact]
        public void LoadByBasetypeCanReturnNull()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                var entity = session.Load<AbstractEntity>(id);
                entity.ShouldBe(null);
            }
        }

        [Fact]
        public void CanQueryByInterface()
        {
            Document<AbstractEntity>().With(x => x.Property);
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = Guid.NewGuid(), Property = "Asger" });
                session.Store(new MoreDerivedEntity2 { Id = Guid.NewGuid(), Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<ISomeInterface>().OrderBy(x => x.Column<string>("Discriminator")).ToList();
                entities.Count().ShouldBe(2);
                entities[0].ShouldBeOfType<MoreDerivedEntity1>();
                entities[1].ShouldBeOfType<MoreDerivedEntity2>();

                var entities2 = session.Query<IOtherInterface>().OrderBy(x => x.Column<string>("Discriminator")).ToList();
                entities2.Count().ShouldBe(1);
                entities[0].ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void CanQueryByBasetype()
        {
            Document<AbstractEntity>().With(x => x.Property);
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = Guid.NewGuid(), Property = "Asger" });
                session.Store(new MoreDerivedEntity2 { Id = Guid.NewGuid(), Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<AbstractEntity>().Where(x => x.Property == "Asger").OrderBy(x => x.Column<string>("Discriminator")).ToList();
                entities.Count().ShouldBe(2);
                entities[0].ShouldBeOfType<MoreDerivedEntity1>();
                entities[1].ShouldBeOfType<MoreDerivedEntity2>();
            }
        }

        [Fact]
        public void CanQueryBySubtype()
        {
            Document<AbstractEntity>().With(x => x.Property);
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = Guid.NewGuid(), Property = "Asger" });
                session.Store(new MoreDerivedEntity2 { Id = Guid.NewGuid(), Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<MoreDerivedEntity2>().Where(x => x.Property == "Asger").ToList();
                entities.Count.ShouldBe(1);
                entities[0].ShouldBeOfType<MoreDerivedEntity2>();
            }
        }

        [Fact]
        public void AutoRegistersSubTypesWhenStoredFirstTime()
        {
            Document<AbstractEntity>().With(x => x.Property);

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id, Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Query<AbstractEntity>().Single(x => x.Property == "Asger");
                entity.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void CanQueryUserDefinedProjection()
        {
            Document<OtherEntity>().With("Unknown", x => 2);

            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new OtherEntity { Id = id });
                session.SaveChanges();

                var entities = session.Query<OtherEntity>().Where(x => x.Column<int>("Unknown") == 2).ToList();
                entities.Count.ShouldBe(1);
            }
        }

        [Fact]
        public void CanQueryIndex()
        {
            Document<AbstractEntity>()
                .Extend<EntityIndex>(e => e.With(x => x.YksiKaksiKolme, x => x.Number))
                .With(x => x.Property);


            var id = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id, Property = "Asger", Number = 2 });
                session.SaveChanges();

                //var test = from x in session.Query<AbstractEntity>()
                //           let index = x.Index<EntityIndex>()
                //           where x.Property == "Asger" && index.YksiKaksiKolme > 1
                //           select x;

                var entity = session.Query<AbstractEntity>().Single(x => x.Property == "Asger" && x.Index<EntityIndex>().YksiKaksiKolme > 1);
                entity.ShouldBeOfType<MoreDerivedEntity1>();
            }
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
    }
}