using System;
using System.Data;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Documentation
{
    /// <summary>
    /// Tests for code samples in: docs/07-documentstore-transaction.md
    /// </summary>
    public class Doc07_TransactionTests : DocumentationTestBase
    {
        public Doc07_TransactionTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void CreateDocumentStore_ForProduction()
        {
            #region CreateDocumentStore_ForProduction
            var newStore = DocumentStore.Create(config =>
            {
                config.UseConnectionString(
                    "Server=localhost;Database=MyApp;Integrated Security=True;Encrypt=False;");
                
                // Configure documents
                config.Document<Product>()
                    .With(x => x.Name)
                    .With(x => x.Price);
                
                config.Document<Order>()
                    .With(x => x.CustomerId)
                    .With(x => x.OrderDate);
            });
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void CreateDocumentStore_ForTesting()
        {
            #region CreateDocumentStore_ForTesting
            var newStore = DocumentStore.ForTesting(
                TableMode.GlobalTempTables,
                config =>
                {
                    config.Document<Product>()
                        .With(x => x.Name);
                }
            );
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void BasicTransaction()
        {
            Document<Product>();
            Document<Order>();

            #region BasicTransaction
            using var tx = store.BeginTransaction();

            using var session = store.OpenSession(tx);

            session.Store(new Product { Id = "product-1", Name = "Widget" });
            session.SaveChanges();

            using var session2 = store.OpenSession(tx);

            session2.Store(new Order { Id = "order-1", ProductId = "product-1" });
            session2.SaveChanges();

            // Commit the transaction
            tx.Complete();
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void TransactionWithIsolationLevel()
        {
            Document<Product>().With(x => x.Stock);

            store.Transactionally(tx =>
            {
                using var setup = store.OpenSession(tx);
                setup.Store(new Product { Id = "product-1", Stock = 10 });
                setup.SaveChanges();
            });

            #region TransactionWithIsolationLevel
            using var tx = store.BeginTransaction(IsolationLevel.Snapshot);

            using var session = store.OpenSession(tx);

            // both loaded in the same snapshot time
            var product1 = session.Load<Product>("product-1"); 
            var product2 = session.Load<Product>("product-2");

            tx.Complete();
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void TransactionWithCommitId()
        {
            Document<Product>();

            #region TransactionWithCommitId
            var commitId = Guid.NewGuid();

            using var tx = store.BeginTransaction(commitId);

            // All changes in this transaction will have the same commit ID
            // Useful for tracking related changes

            using var session = store.OpenSession(tx);

            var product = new Product { Id = "p1", Name = "Widget" };
            session.Store(product);
            session.SaveChanges();

            tx.Complete();
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void TransactionWithConnectionTimeout()
        {
            Document<Product>();

            #region TransactionWithConnectionTimeout
            using (var tx = store.BeginTransaction(
                IsolationLevel.ReadCommitted, 
                connectionTimeout: TimeSpan.FromSeconds(30)))
            {
                // Transaction will fail if connection can't be acquired within 30 seconds
                
                using var session = store.OpenSession(tx);

                // ...
                
                tx.Complete();
            }
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void TransactionRollback()
        {
            Document<Product>();
            Document<Order>();

            #region TransactionRollback
            using var tx = store.BeginTransaction();

            try
            {
                using (var session = store.OpenSession(tx))
                {
                    var product = new Product { Id = "p1", Name = "Widget" };
                    session.Store(product);
                    session.SaveChanges();
                }
                
                // Some operation that might fail
                ProcessPayment();
                
                tx.Complete();  // Only commits if we reach here
            }
            catch (Exception)
            {
                // Transaction automatically rolls back on dispose
                throw;
            }
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void MultiSessionTransaction()
        {
            Document<Product>().With(x => x.Stock);
            Document<Order>();

            store.Transactionally(tx =>
            {
                using var setup = store.OpenSession(tx);
                setup.Store(new Product { Id = "product-1", Stock = 10 });
                setup.SaveChanges();
            });

            #region MultiSessionTransaction
            using (var tx = store.BeginTransaction())
            {
                // Session 1: Update product
                using (var session1 = store.OpenSession(tx))
                {
                    var product = session1.Load<Product>("product-1");
                    product.Stock--;
                    session1.SaveChanges();
                }

                // Session 2: Create order
                using (var session2 = store.OpenSession(tx))
                {
                    var order = new Order 
                    { 
                        Id = Guid.NewGuid().ToString(),
                        ProductId = "product-1",
                        Quantity = 1
                    };
                    session2.Store(order);
                    session2.SaveChanges();
                }

                // Both operations committed together
                tx.Complete();
            }
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void IdempotentOperations()
        {
            Document<Order>();
            Document<Product>().With(x => x.Stock);

            var messageId = Guid.NewGuid();
            
            store.Transactionally(tx =>
            {
                using var setup = store.OpenSession(tx);
                setup.Store(new Product { Id = "p1", Name = "Widget", Stock = 100 });
                setup.SaveChanges();
            });

            #region IdempotentOperations
            // Use message ID from external system as commitId
            using var tx = store.BeginTransaction(messageId);
            using var session = store.OpenSession(tx);

            // Process the message
            var order = new Order { Id = "order-1", ProductId = "p1", Quantity = 5 };
            session.Store(order);

            var inventory = session.Load<Product>(order.ProductId);
            inventory.Stock -= order.Quantity;

            session.SaveChanges();
            tx.Complete();

            // All documents modified will have Etag == messageId (if they are not changed again later)
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void ShortTransaction_Good()
        {
            Document<Product>().With(x => x.Price);

            store.Transactionally(tx =>
            {
                using var setup = store.OpenSession(tx);
                setup.Store(new Product { Id = "product-1", Price = 50.00m });
                setup.SaveChanges();
            });

            #region ShortTransaction_Good
            // Good: Quick transaction
            using (var tx = store.BeginTransaction())
            {
                using var session = store.OpenSession(tx);

                var product = session.Load<Product>("product-1");
                product.Price = 99.99m;
                session.SaveChanges();
                
                tx.Complete();
            }
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public async Task ConcurrencyExceptionRetry()
        {
            Document<Product>().With(x => x.Stock);

            var id = "product-1";
            store.Transactionally(tx =>
            {
                using var session = store.OpenSession(tx);
                session.Store(new Product { Id = id, Stock = 10 });
                session.SaveChanges();
            });

            #region ConcurrencyExceptionRetry
            int retries = 3;
            while (retries-- > 0)
            {
                try
                {
                    using (var session = store.OpenSession())
                    {
                        var product = session.Load<Product>(id);
                        var etag = session.Advanced.GetEtagFor(product);
                        
                        product.Stock--;
                        session.Store(id, product, etag);
                        session.SaveChanges();
                        break;
                    }
                }
                catch (ConcurrencyException) when (retries > 0)
                {
                    // Retry with exponential backoff
                    await Task.Delay(100 * (3 - retries));
                }
            }
            #endregion
        }

        [Fact(Skip = "Code example - not meant for execution")]
        public void RelatedChangesTransaction()
        {
            Document<Product>().With(x => x.Stock);
            Document<Order>();

            var productId = "product-1";
            store.Transactionally(tx =>
            {
                using var session = store.OpenSession(tx);
                session.Store(new Product { Id = productId, Stock = 10 });
                session.SaveChanges();
            });

            #region RelatedChangesTransaction
            // Good: Transaction ensures both updates succeed or fail together
            using (var tx = store.BeginTransaction())
            {
                using var session = store.OpenSession(tx);

                var product = session.Load<Product>(productId);
                product.Stock--;
                
                var order = new Order { ProductId = productId, Quantity = 1 };
                session.Store(order);
                
                session.SaveChanges();
                
                tx.Complete();
            }
            #endregion
        }
    }
}
