using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace HybridDb.Tests;

public class DocumentSession_ConcurrencyTests
{
    [Fact]
    public void AutomaticConcurrencyChecking()
    {
        #region Concurrency_AutomaticChecking
        using var session = store.OpenSession();

        // Load automatically captures the current Etag
        var product = session.Load<Product>("product-1");

        // Make changes
        product.Price = 99.99m;

        // SaveChanges() automatically checks that Etag hasn't changed
        // Throws ConcurrencyException if another process modified the document
        session.SaveChanges();
        #endregion
    }

    [Fact]
    public void DisablingConcurrencyChecks()
    {
        #region Concurrency_DisablingChecks
        using var session = store.OpenSession();

        var product = session.Load<Product>("product-1");
        product.Price = 99.99m;

        // Override any concurrent changes - no ConcurrencyException
        session.SaveChanges(lastWriteWins: true, forceWriteUnchangedDocument: false);
        #endregion
    }

    [Fact]
    public void ManualEtagManagement()
    {
        #region Concurrency_ManualEtagManagement
        using var session = store.OpenSession();

        // Get the current Etag (e.g., from a web request)
        Guid? etag = GetEtagFromClientRequest();

        // Load and modify
        var product = session.Load<Product>("product-1");
        product.Price = 99.99m;

        // Store with explicit Etag - overrides the one captured during Load
        session.Store("product-1", product, etag);
        session.SaveChanges();  // Uses the explicitly provided Etag
        #endregion
    }

    [Fact]
    public void NewDocuments()
    {
        #region Concurrency_NewDocuments
        using var session = store.OpenSession();

        // Store new document - marked as Transient (will INSERT)
        session.Store(new Product { Id = "product-1", Name = "Widget" });

        // SaveChanges() performs INSERT - no Etag check needed
        session.SaveChanges();
        #endregion
    }

    [Fact]
    public void PerOperationTransactions()
    {
        #region Concurrency_PerOperationTransactions
        using var session = store.OpenSession();

        var product = session.Load<Product>("p1");  // Transaction 1: Read at time T1
        // ... time passes, other processes may modify the document ...
        product.Price = 99.99m;
        session.SaveChanges();                       // Transaction 2: Write at time T2, checks Etag from T1
        #endregion
    }

    [Fact]
    public void VerifyCommitIds()
    {
        #region Concurrency_VerifyCommitIds
        using var session = store.OpenSession();

        var order = session.Load<Order>("order-1");
        var product = session.Load<Product>(order.ProductId);
        
        var orderEtag = session.Advanced.GetEtagFor(order);
        var productEtag = session.Advanced.GetEtagFor(product);
        
        if (orderEtag != productEtag)
        {
            // Product changed since order was created - handle appropriately
        }
        #endregion
    }

    [Fact]
    public void DocumentTransaction()
    {
        #region Concurrency_DocumentTransaction
        using var tx = store.BeginTransaction(IsolationLevel.RepeatableRead);
        using var session = store.OpenSession(tx);

        var product = session.Load<Product>("p1");  // Uses tx, acquires locks based on isolation level
        product.Price = 99.99m;
        session.SaveChanges();                       // Uses same tx, Etag check still performed
        tx.Complete();
        #endregion
    }

    [Fact]
    public void ReadCalculateWrite()
    {
        #region Concurrency_ReadCalculateWrite
        using var session = store.OpenSession();

        var product = session.Load<Product>("p1");  
        var inventory = session.Load<Inventory>("inv-1");

        // Calculate based on both
        if (inventory.Stock >= product.MinimumStock)
        {
            inventory.Stock -= product.MinimumStock;
            // SaveChanges() will check Etags - but if another process modified inventory,
            // we'll get ConcurrencyException and need to retry
            session.SaveChanges();
        }
        #endregion
    }

    [Fact]
    public void ReadCalculateWriteWithTransaction()
    {
        #region Concurrency_ReadCalculateWriteWithTransaction
        using var tx = store.BeginTransaction(IsolationLevel.Serializable);
        using var session = store.OpenSession(tx);

        var product = session.Load<Product>("p1");  // Acquires read lock
        var inventory = session.Load<Inventory>("inv-1");  // Acquires read lock

        if (inventory.Stock >= product.MinimumStock)
        {
            inventory.Stock -= product.MinimumStock;
            session.SaveChanges();  // Etag check still performed, plus transaction guarantees
        }

        tx.Complete();  // No other process could have modified these documents during the transaction
        #endregion
    }

    [Fact]
    public void SessionCaching()
    {
        #region Concurrency_SessionCaching
        using var session = store.OpenSession();

        var product = session.Load<Product>("p1");  
        Console.WriteLine(product.Price);  // 100

        // Another process updates product.Price to 150

        var productAgain = session.Load<Product>("p1");  
        Console.WriteLine(productAgain.Price);  // Still 100 (from session cache)
        #endregion
    }

    [Fact]
    public void MultipleSessionsWithoutTransaction()
    {
        #region Concurrency_MultipleSessions
        // Problem: Each session runs in its own transaction
        int requiredStock;
        using (var session1 = store.OpenSession())
        {
            var product = session1.Load<Product>("p1");
            requiredStock = product.MinimumStock;  // Read at T1
            session1.SaveChanges();
        }

        using (var session2 = store.OpenSession())
        {
            var inventory = session2.Load<Inventory>("inv-1");
            
            // Decision based on data from session1, but inventory may have changed
            // between session1 and session2, causing inconsistent decisions
            if (inventory.Stock >= requiredStock)
            {
                inventory.Stock -= requiredStock;
                session2.SaveChanges();
            }
        }
        #endregion
    }

    [Fact]
    public void MultipleSessionsWithTransaction()
    {
        #region Concurrency_MultiSessionsWithTransaction
        using (var tx = store.BeginTransaction())
        {
            using (var session1 = store.OpenSession(tx))
            {
                var product = session1.Load<Product>("p1");
                product.Stock--;
                session1.SaveChanges();
            }
            
            using (var session2 = store.OpenSession(tx))
            {
                var order = new Order { ProductId = "p1" };
                session2.Store(order);
                session2.SaveChanges();
            }
            
            tx.Complete();  // Both sessions' changes committed together
        }
        #endregion
    }

    [Fact]
    public void HighContention()
    {
        #region Concurrency_HighContention
        // High contention - automatic Etag checking causes retries
        int retries = 10;
        while (retries-- > 0)
        {
            try
            {
                using (var session = store.OpenSession())
                {
                    var counter = session.Load<Counter>("global-counter");
                    // Etag is automatically captured
                    
                    counter.Value++;
                    session.SaveChanges();  // Throws ConcurrencyException if another process modified it
                    break;
                }
            }
            catch (ConcurrencyException)
            {
                // Retry with backoff...
            }
        }
        #endregion
    }

    [Fact]
    public void PessimisticLocking()
    {
        #region Concurrency_PessimisticLocking
        using (var tx = store.BeginTransaction(IsolationLevel.Serializable))
        {
            using (var session = store.OpenSession(tx))
            {
                var counter = session.Load<Counter>("global-counter");  // Acquires exclusive lock
                counter.Value++;  // No other process can read or write this document
                session.SaveChanges();  // Etag still checked, transaction provides additional locking
            }
            
            tx.Complete();
        }
        #endregion
    }

    [Fact]
    public void SingleSessionAtomicity()
    {
        #region Concurrency_SingleSessionAtomicity
        // Good: Atomic within single SaveChanges
        using (var session = store.OpenSession())
        {
            var product = session.Load<Product>("p1");
            product.Stock--;
            
            var order = new Order { ProductId = "p1" };
            session.Store(order);
            
            session.SaveChanges();  // Both changes or neither
        }
        #endregion
    }

    #region Concurrency_RetryLogic
    async Task<bool> UpdateWithRetry<T>(string id, Action<T> update, int maxRetries = 3) where T : class
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using (var session = store.OpenSession())
                {
                    var entity = session.Load<T>(id);
                    // Etag automatically captured during Load
                    
                    update(entity);
                    
                    session.SaveChanges();  // Automatic Etag check
                    return true;
                }
            }
            catch (ConcurrencyException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100));
            }
        }
        return false;
    }
    #endregion

    // Placeholder classes for the examples
    public class Product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int MinimumStock { get; set; }
        public int Stock { get; set; }
    }

    public class Order
    {
        public string Id { get; set; }
        public string ProductId { get; set; }
        public object ProductSnapshot { get; set; }
    }

    public class Inventory
    {
        public string Id { get; set; }
        public int Stock { get; set; }
    }

    public class Counter
    {
        public string Id { get; set; }
        public int Value { get; set; }
    }

    private IDocumentStore store;

    private Guid? GetEtagFromClientRequest() => null;
}
