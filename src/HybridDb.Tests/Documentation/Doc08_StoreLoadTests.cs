using System;
using System.Linq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Documentation
{
    /// <summary>
    /// Tests for code samples in: docs/08-documentsession-store-load.md
    /// </summary>
    public class Doc08_StoreLoadTests : DocumentationTestBase
    {
        public Doc08_StoreLoadTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void BasicSession()
        {
            Document<Product>();

            #region BasicSession
            using var session = store.OpenSession();

            // Work with documents
            session.Store(new Product { Id = "p1", Name = "Widget" });
            session.SaveChanges();
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void SessionWithTransaction()
        {
            Document<Product>();

            #region SessionWithTransaction
            using var tx = store.BeginTransaction();

            using var session = store.OpenSession(tx);

            // Work with documents
            session.Store(new Product { Id = "p1", Name = "Widget" });
            session.SaveChanges();

            tx.Complete();
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void StoreNewDocument()
        {
            Document<Product>();

            #region StoreNewDocument
            using var session = store.OpenSession();

            // Auto-generated ID (from Id property)
            var product = new Product 
            { 
                Id = Guid.NewGuid().ToString(),
                Name = "Widget",
                Price = 19.99m
            };

            session.Store(product);
            session.SaveChanges();
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void StoreWithExplicitKey()
        {
            Document<Product>();

            #region StoreWithExplicitKey
            using var session = store.OpenSession();

            var product = new Product 
            { 
                Name = "Widget",
                Price = 19.99m
            };

            // Specify the key explicitly
            session.Store("product-123", product);
            session.SaveChanges();
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void StoreWithEtag()
        {
            Document<Product>().With(x => x.Price);

            store.Transactionally(tx =>
            {
                using var setup = store.OpenSession(tx);
                setup.Store("product-123", new Product { Name = "Widget", Price = 19.99m });
                setup.SaveChanges();
            });

            #region StoreWithEtag
            using var session = store.OpenSession();
            var product = session.Load<Product>("product-123");
            var etag = session.Advanced.GetEtagFor(product);
                
            product.Price = 24.99m;
                
            // Store with etag for optimistic concurrency
            session.Store("product-123", product, etag);
                
            try
            {
                session.SaveChanges();
            }
            catch (ConcurrencyException)
            {
                // Document was modified by another process
                // Handle the conflict
            }
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void StoreMultipleDocuments()
        {
            Document<Product>();

            #region StoreMultipleDocuments
            using var session = store.OpenSession();

            var products = new[]
            {
                new Product { Id = "p1", Name = "Widget" },
                new Product { Id = "p2", Name = "Gadget" },
                new Product { Id = "p3", Name = "Gizmo" }
            };

            foreach (var product in products)
            {
                session.Store(product);
            }

            session.SaveChanges();
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void LoadSingleDocument()
        {
            Document<Product>();

            store.Transactionally(tx =>
            {
                using var setup = store.OpenSession(tx);
                setup.Store("product-123", new Product { Name = "Widget" });
                setup.SaveChanges();
            });

            #region LoadSingleDocument
            using var session = store.OpenSession();

            var product = session.Load<Product>("product-123");

            if (product != null)
            {
                output.WriteLine($"Found: {product.Name}");
            }
            #endregion
            
            product.ShouldNotBeNull();
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void LoadMultipleDocuments()
        {
            Document<Product>();

            store.Transactionally(tx =>
            {
                using var setup = store.OpenSession(tx);
                setup.Store(new Product { Id = "product-1", Name = "Widget" });
                setup.Store(new Product { Id = "product-2", Name = "Gadget" });
                setup.Store(new Product { Id = "product-3", Name = "Gizmo" });
                setup.SaveChanges();
            });

            #region LoadMultipleDocuments
            using var session = store.OpenSession();

            var ids = new[] { "product-1", "product-2", "product-3" };
            var products = session.Load<Product>(ids);

            output.WriteLine($"Loaded {products.Count} products");
            #endregion
            
            products.Count.ShouldBe(3);
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void LoadAsReadOnly()
        {
            Document<Product>();

            store.Transactionally(tx =>
            {
                using var setup = store.OpenSession(tx);
                setup.Store("product-123", new Product { Name = "Widget" });
                setup.SaveChanges();
            });

            #region LoadAsReadOnly
            using var session = store.OpenSession();
            var product = session.Load<Product>("product-123", readOnly: true);
                
            // product can be read but not modified
            output.WriteLine(product.Name);
            #endregion
            
            product.ShouldNotBeNull();
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void LoadMultipleAsReadOnly()
        {
            Document<Product>();

            store.Transactionally(tx =>
            {
                using var setup = store.OpenSession(tx);
                setup.Store(new Product { Id = "p1", Name = "Widget" });
                setup.Store(new Product { Id = "p2", Name = "Gadget" });
                setup.Store(new Product { Id = "p3", Name = "Gizmo" });
                setup.SaveChanges();
            });

            #region LoadMultipleAsReadOnly
            using var session = store.OpenSession();

            var ids = new[] { "p1", "p2", "p3" };
            var products = session.Load<Product>(ids, readOnly: true);

            foreach (var product in products)
            {
                output.WriteLine(product.Name);
            }
            #endregion
            
            products.Count.ShouldBe(3);
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void BasicSave()
        {
            Document<Product>();

            #region BasicSave
            using var session = store.OpenSession();

            session.Store(new Product { Id = "p1", Name = "Widget" });

            var commitId = session.SaveChanges();
            output.WriteLine($"Saved with commit ID: {commitId}");
            #endregion
            
            commitId.ShouldNotBe(Guid.Empty);
        }
    }
}
