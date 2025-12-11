using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Documentation
{
    /// <summary>
    /// Tests for code samples in: docs/01-getting-started.md
    /// </summary>
    public class Doc01_GettingStartedTests : DocumentationTestBase
    {
        public Doc01_GettingStartedTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void QuickStart_BasicExample()
        {
            // #region QuickStart_BasicExample
            // Create a document store for testing (uses temp tables)
            Document<Entity>();
            Document<Entity>().With(x => x.Property);

            // Use the store
            using var session = store.OpenSession();
            
            // Store a document
            session.Store(new Entity 
            { 
                Id = Guid.NewGuid().ToString(), 
                Property = "Hello", 
                Number = 2001 
            });

            session.SaveChanges();
            // #endregion

            // Query documents using LINQ
            using var session2 = store.OpenSession();
            var entity = session2.Query<Entity>()
                .Single(x => x.Property == "Hello");

            // Update the entity
            entity.Number++;
            session2.SaveChanges();
            
            // Verify
            entity.Number.ShouldBe(2002);
        }

        [Fact]
        public void ProductionSetup()
        {
            // #region ProductionSetup
            var store = DocumentStore.Create(configuration =>
            {
                configuration.UseConnectionString(
                    "Server=localhost;Database=MyApp;Integrated Security=True;Encrypt=False;");
                
                // Configure documents
                configuration.Document<Product>()
                    .With(x => x.Name)
                    .With(x => x.Price);
                
                configuration.Document<Order>()
                    .With(x => x.CustomerId)
                    .With(x => x.OrderDate);
            }, initialize: false);
            // #endregion

            store.ShouldNotBeNull();
        }

        [Fact]
        public void DocumentConfiguration()
        {
            // #region DocumentConfiguration
            Document<Product>()
                .With(x => x.Name)           // Index the Name property
                .With(x => x.Price)          // Index the Price property
                .With(x => x.CategoryId);    // Index the CategoryId property
            // #endregion
        }

        [Fact]
        public void RepositoryPattern()
        {
            // #region RepositoryPattern
            Document<Product>().With(x => x.Name).With(x => x.Category);

            var repository = new ProductRepository(store);
            
            var product = new Product { Id = "p1", Name = "Widget", Price = 99.99m, Category = "Tools" };
            repository.Save(product);
            
            var loaded = repository.GetById("p1");
            loaded.Name.ShouldBe("Widget");
            
            var products = repository.FindByCategory("Tools");
            products.Count.ShouldBe(1);
            // #endregion
        }

        [Fact]
        public void UsingTransactions()
        {
            Document<Entity>();

            // #region UsingTransactions
            // Begin a transaction
            using var tx = store.BeginTransaction();

            try
            {
                using var session1 = store.OpenSession(tx);
                var entity = session1.Load<Entity>("some-id");
                if (entity != null)
                {
                    entity.Property = "Updated";
                    session1.SaveChanges();
                }

                using var session2 = store.OpenSession(tx);
                session2.Store(new Entity { Id = Guid.NewGuid().ToString(), Property = "New" });
                session2.SaveChanges();

                // Commit the transaction
                tx.Complete();
            }
            catch
            {
                // Transaction rolls back automatically
                throw;
            }
            // #endregion
        }

        // Sample repository class for documentation
        public class ProductRepository
        {
            private readonly IDocumentStore store;

            public ProductRepository(IDocumentStore store)
            {
                this.store = store;
            }

            public Product GetById(string id)
            {
                using var session = store.OpenSession();
                return session.Load<Product>(id);
            }

            public void Save(Product product)
            {
                using var session = store.OpenSession();
                session.Store(product);
                session.SaveChanges();
            }

            public IList<Product> FindByCategory(string category)
            {
                using var session = store.OpenSession();
                return session.Query<Product>()
                    .Where(x => x.Category == category)
                    .ToList();
            }
        }
    }
}
