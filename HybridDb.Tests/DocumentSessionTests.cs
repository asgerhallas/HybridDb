using System;
using System.Data.SqlClient;
using Dapper;
using Shouldly;
using Xunit;
using System.Linq;

namespace HybridDb.Tests
{
    public class DocumentSessionTests : IDisposable
    {
        readonly SqlConnection connection;
        readonly IDocumentStore store;

        public DocumentSessionTests()
        {
            connection = new SqlConnection("data source=.;Integrated Security=True");
            connection.Open();
            store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>();
            store.Initialize();
        }

        public void Dispose()
        {
            connection.Dispose();
        }

        [Fact]
        public void CanOpenSession()
        {
            store.OpenSession().ShouldNotBe(null);
        }

        [Fact]
        public void CannotOpenSessionIfStoreIsNotInitilized()
        {
            Should.Throw<DocumentStore.StoreNotInitializedException>(() => DocumentStore.ForTesting(connection).OpenSession());
        }

        [Fact]
        public void CanStoreDocument()
        {
            using (var session = store.OpenSession())
            {
                session.Store(new Entity
                {
                    Property = "Asger"
                });
                session.SaveChanges();
            }

            var entity = connection.Query("select * from #Entity").SingleOrDefault();
            Assert.NotNull(entity);
            Assert.NotNull(entity.Document);
            Assert.NotEqual(0, entity.Document.Length);
        }

        [Fact]
        public void StoresIdRetrievedFromObject()
        {
            using (var session = store.OpenSession())
            {
                session.Store(new Entity());
                session.SaveChanges();
            }
        }

        [Fact]
        public void StoringDocumentWithSameIdTwiceIsIgnored()
        {
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
        public void CanDeleteDocument()
        {
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
        public void CanClearSession()
        {
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
            using (var session = store.OpenSession())
            {
                session.Load<Entity>(Guid.NewGuid()).ShouldBe(null);
            }
        }

        [Fact]
        public void SavesChangesWhenObjectHasChanged()
        {
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

        public class Entity
        {
            public Guid Id { get; set; }
            public string Property { get; set; }
        }
    }
}