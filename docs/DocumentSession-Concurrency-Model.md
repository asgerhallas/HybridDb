# HybridDb Concurrency Model

## Overview

HybridDb uses **optimistic concurrency control** by default for write operations. This approach minimizes database locks and maximizes performance by assuming conflicts are rare and detecting them when they occur, rather than preventing them upfront with locks.

## How Optimistic Concurrency Works

### ETags (Entity Tags)

Every document in HybridDb has an `Etag` column containing a unique identifier (GUID) that changes whenever the document is modified. When you save a document with an ETag check, HybridDb verifies that the ETag hasn't changed since you loaded it.

```csharp
using var session = store.OpenSession();

// Load gets current document + its ETag
var product = session.Load<Product>("product-1");
var etag = session.Advanced.GetEtagFor(product);

// Make changes
product.Price = 99.99m;

// Save with ETag check - fails if document was modified by another process
session.Store("product-1", product, etag);
session.SaveChanges();  // Throws ConcurrencyException if ETag mismatch
```

### Without Explicit ETag Check

When you don't provide an ETag, SaveChanges() performs a "last-write-wins" update:

```csharp
using var session = store.OpenSession();

var product = session.Load<Product>("product-1");
product.Price = 99.99m;

// No ETag check - always succeeds, overwrites any concurrent changes
session.SaveChanges();
```

## Transaction Behavior and Concurrency

### Default: Per-Operation Transactions

By default, each operation (Load, SaveChanges) runs in its own short-lived transaction:

```csharp
using var session = store.OpenSession();

var product = session.Load<Product>("p1");  // Transaction 1: Read at time T1
// ... time passes, other processes may modify the document ...
product.Price = 99.99m;
session.SaveChanges();                       // Transaction 2: Write at time T2
```

**Implication**: The document you loaded at T1 might have been modified by the time you save at T2. HybridDb relies on optimistic concurrency (ETags) to detect this, not pessimistic locking.

### With DocumentTransaction: Session-Lifetime Transaction

When you provide a DocumentTransaction, all operations share the same transaction with consistent isolation:

```csharp
using var tx = store.BeginTransaction(IsolationLevel.RepeatableRead);
using var session = store.OpenSession(tx);

var product = session.Load<Product>("p1");  // Uses tx, locks based on isolation level
product.Price = 99.99m;
session.SaveChanges();                       // Uses same tx
tx.Complete();
```

**Implication**: Higher isolation levels (RepeatableRead, Serializable) acquire locks to ensure consistency, which can impact performance and concurrency.

## Advantages of Optimistic Concurrency

### High Performance
- **Minimal locking**: Read operations don't acquire locks
- **No lock waits**: Processes don't wait for each other during reads
- **High concurrency**: Multiple processes can read and modify different documents simultaneously
- **Scalability**: Works well under high load with many concurrent users

### Suitable for Most Scenarios
- **Low contention**: When different users typically work on different documents
- **Read-heavy workloads**: Reads are fast and don't interfere with each other
- **Web applications**: Natural fit for HTTP's stateless request model
- **Document databases**: Natural model where each document is an isolated unit

## When Optimistic Concurrency Is Not Enough

### 1. Consistent Queries Within a Session

**Problem**: Multiple loads or queries in the same session may see different snapshots:

```csharp
using var session = store.OpenSession();

// Load 1: Transaction 1
var product = session.Load<Product>("p1");  
Console.WriteLine(product.Price);  // 100

// Another process updates product.Price to 150

// Load 2: Transaction 2 - sees the updated value!
var productAgain = session.Load<Product>("p1");  
Console.WriteLine(productAgain.Price);  // 150 (different from first load!)
```

**Solution**: Use a DocumentTransaction for consistent snapshot:

```csharp
using var tx = store.BeginTransaction(IsolationLevel.RepeatableRead);
using var session = store.OpenSession(tx);

var product = session.Load<Product>("p1");  
Console.WriteLine(product.Price);  // 100

// Another process tries to update - blocked or sees old value depending on isolation

var productAgain = session.Load<Product>("p1");  
Console.WriteLine(productAgain.Price);  // 100 (consistent within transaction)

tx.Complete();
```

### 2. Read-Calculate-Write Operations

**Problem**: You need to read a value, calculate based on it, then write back without interference:

```csharp
using var session = store.OpenSession();

var product = session.Load<Product>("p1");  
var inventory = session.Load<Inventory>("inv-1");

// Calculate based on both
if (inventory.Stock >= product.MinimumStock)
{
    // By the time we save, inventory.Stock might have changed!
    inventory.Stock -= product.MinimumStock;
    session.SaveChanges();
}
```

**Solution**: Use a transaction with appropriate isolation level:

```csharp
using var tx = store.BeginTransaction(IsolationLevel.Serializable);
using var session = store.OpenSession(tx);

var product = session.Load<Product>("p1");  
var inventory = session.Load<Inventory>("inv-1");

if (inventory.Stock >= product.MinimumStock)
{
    inventory.Stock -= product.MinimumStock;
    session.SaveChanges();
}

tx.Complete();  // Ensures no concurrent modifications during the operation
```

### 3. Coordinating Multiple Related Writes

**Problem**: You need to ensure multiple documents are updated together:

```csharp
// Without transaction - these could fail independently
using (var session1 = store.OpenSession())
{
    var product = session1.Load<Product>("p1");
    product.Stock--;
    session1.SaveChanges();  // Succeeds
}

using (var session2 = store.OpenSession())
{
    var order = new Order { ProductId = "p1" };
    session2.Store(order);
    session2.SaveChanges();  // Fails - product.Stock decremented but no order!
}
```

**Solution**: Use a single transaction:

```csharp
using (var tx = store.BeginTransaction())
{
    using (var session = store.OpenSession(tx))
    {
        var product = session.Load<Product>("p1");
        product.Stock--;
        
        var order = new Order { ProductId = "p1" };
        session.Store(order);
        
        session.SaveChanges();
    }
    
    tx.Complete();  // Both changes committed together, or neither
}
```

### 4. High-Contention Scenarios

**Problem**: Multiple processes frequently modify the same documents, causing many ConcurrencyException failures:

```csharp
// High contention - many retries may be needed
int retries = 10;
while (retries-- > 0)
{
    try
    {
        using (var session = store.OpenSession())
        {
            var counter = session.Load<Counter>("global-counter");
            var etag = session.Advanced.GetEtagFor(counter);
            
            counter.Value++;
            session.Store("global-counter", counter, etag);
            session.SaveChanges();
            break;
        }
    }
    catch (ConcurrencyException)
    {
        // Retry...
    }
}
```

**Solution**: Use pessimistic locking with higher isolation level:

```csharp
using (var tx = store.BeginTransaction(IsolationLevel.Serializable))
{
    using (var session = store.OpenSession(tx))
    {
        var counter = session.Load<Counter>("global-counter");
        counter.Value++;  // Locks the row, prevents concurrent modifications
        session.SaveChanges();
    }
    
    tx.Complete();
}
```

## Choosing the Right Approach

### Use Default Optimistic Concurrency When:
- ✅ Different users work on different documents
- ✅ Contention is low
- ✅ Workload is read-heavy
- ✅ Single operations per session (load → modify → save)
- ✅ Performance and scalability are priorities

### Use DocumentTransaction When:
- ⚠️ Need consistent view across multiple loads/queries in a session
- ⚠️ Coordinating multiple related writes
- ⚠️ Read-calculate-write operations
- ⚠️ High contention on specific documents
- ⚠️ Need to integrate with other SQL operations

### Isolation Level Guidelines

**ReadCommitted (default)**
- Good for most scenarios
- Prevents dirty reads
- Allows non-repeatable reads and phantoms
- Minimal locking

**RepeatableRead**
- Ensures consistent reads within transaction
- Prevents dirty and non-repeatable reads
- Still allows phantoms (new rows)
- Moderate locking

**Serializable**
- Highest isolation
- Prevents dirty reads, non-repeatable reads, and phantoms
- Most locking - can impact performance
- Use for critical operations requiring absolute consistency

**Snapshot**
- Uses row versioning instead of locks
- No read locks, high concurrency
- Consistent snapshot without blocking
- Requires database configuration (`ALTER DATABASE ... SET ALLOW_SNAPSHOT_ISOLATION ON`)

## Best Practices

### 1. Default to Optimistic Concurrency

Let HybridDb handle transactions automatically for single operations:

```csharp
using var session = store.OpenSession();
var product = session.Load<Product>("p1");
product.Price = 99.99m;
session.SaveChanges();  // Fast, no locks, works for most cases
```

### 2. Use ETags for Critical Updates

When you need to ensure no concurrent modifications:

```csharp
using var session = store.OpenSession();
var product = session.Load<Product>("p1");
var etag = session.Advanced.GetEtagFor(product);

product.Stock--;  // Critical: must not have changed

session.Store("p1", product, etag);
try
{
    session.SaveChanges();
}
catch (ConcurrencyException)
{
    // Handle conflict - reload and retry, or notify user
}
```

### 3. Use DocumentTransaction Sparingly

Only when you need session-level consistency or cross-session coordination:

```csharp
// Only when needed for consistency
using var tx = store.BeginTransaction(IsolationLevel.RepeatableRead);
using var session = store.OpenSession(tx);

var product = session.Load<Product>("p1");
var relatedData = session.Load<RelatedData>("r1");
// Both see consistent snapshot

session.SaveChanges();
tx.Complete();
```

### 4. Keep Transactions Short

Minimize transaction duration to reduce lock contention:

```csharp
// Good: Short transaction
using (var tx = store.BeginTransaction())
{
    using var session = store.OpenSession(tx);
    var product = session.Load<Product>("p1");
    product.Stock--;
    session.SaveChanges();
    tx.Complete();
}

// Bad: Long transaction
using (var tx = store.BeginTransaction())
{
    var data = await CallExternalApi();  // Don't do this in transaction!
    
    using var session = store.OpenSession(tx);
    session.Store(ConvertToProduct(data));
    session.SaveChanges();
    tx.Complete();
}
```

### 5. Handle ConcurrencyException Gracefully

Implement retry logic with exponential backoff:

```csharp
async Task<bool> UpdateWithRetry<T>(string id, Action<T> update, int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            using (var session = store.OpenSession())
            {
                var entity = session.Load<T>(id);
                var etag = session.Advanced.GetEtagFor(entity);
                
                update(entity);
                
                session.Store(id, entity, etag);
                session.SaveChanges();
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
```

## Summary

HybridDb's optimistic concurrency model provides:
- **High performance** through minimal locking
- **Excellent scalability** for typical workloads
- **Simple programming model** for common scenarios
- **Flexibility** to use pessimistic locking when needed via DocumentTransaction

Choose optimistic concurrency by default, and reach for DocumentTransaction only when you need guaranteed consistency across operations or high isolation levels for specific scenarios.
