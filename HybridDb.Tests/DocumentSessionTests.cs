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
            connection = new SqlConnection("data source=.;Initial Catalog=Energy10;Integrated Security=True");
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
            throw new NotImplementedException();
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
        public void StoringAlreadyPersistenDocumentIsIgnored()
        {
            
        }

        [Fact]
        public void SessionWillRollBackIfSaveChangesNotCalled()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void CanDeleteDocument()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void DeletingANonExistingDocumentIsIgnored()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void CanLoadDocument()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void LoadingANonExistingDocumentReturnsNull()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void SavesChangesWhenObjectHasChanged()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void CanBatchMultipleChanges()
        {
            throw new NotImplementedException();
        }

        public class Entity
        {
            public Guid Id { get; set; }
            public string Property { get; set; }
        }
    }
}