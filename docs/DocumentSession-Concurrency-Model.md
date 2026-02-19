# HybridDb Concurrency Model

## Overview

HybridDb uses **optimistic concurrency control** by default for write operations. This approach minimizes database locks and maximizes performance by assuming conflicts are rare and detecting them when they occur, rather than preventing them upfront with locks.

## How Optimistic Concurrency Works

### ETags (Entity Tags) and CommitId

Every document in HybridDb has an `Etag` column containing a unique identifier (GUID) that changes whenever the document is modified. The Etag is automatically captured when you load a document and is used to detect concurrent modifications.

ETags are also called CommitIds and will be fully renamed to CommitId in the future. 

### Automatic Concurrency Checking

**When you load and modify a document**, HybridDb automatically checks for concurrent changes:

<!-- snippet: Concurrency_AutomaticChecking -->
<a id='snippet-Concurrency_AutomaticChecking'></a>

```cs
using var session = store.OpenSession();

// Load automatically captures the current Etag
var product = session.Load<Product>("product-1");

// Make changes
product.Price = 99.99m;

// SaveChanges() automatically checks that Etag hasn't changed
// Throws ConcurrencyException if another process modified the document
session.SaveChanges();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L21-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_AutomaticChecking' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**What happens internally:**
1. `Load()` retrieves the document and its Etag from the database
2. The session tracks the entity and its original Etag
3. `SaveChanges()` executes an UPDATE with a WHERE clause checking the Etag
4. If the Etag doesn't match (document was modified by someone else in the time window between 1 and 3), a `ConcurrencyException` is thrown

### Disabling Concurrency Checks

You can explicitly disable the Etag check using the `lastWriteWins` parameter:

<!-- snippet: Concurrency_DisablingChecks -->
<a id='snippet-Concurrency_DisablingChecks'></a>

```cs
using var session = store.OpenSession();

var product = session.Load<Product>("product-1");
product.Price = 99.99m;

// Override any concurrent changes - no ConcurrencyException
session.SaveChanges(lastWriteWins: true, forceWriteUnchangedDocument: false);
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L39-L47' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_DisablingChecks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Manual Etag Management

For advanced scenarios, you can explicitly provide an Etag when storing:

<!-- snippet: Concurrency_ManualEtagManagement -->
<a id='snippet-Concurrency_ManualEtagManagement'></a>

```cs
using var session = store.OpenSession();

// Get the current Etag (e.g., from a web request)
Guid? etag = GetEtagFromClientRequest();

// Load and modify
var product = session.Load<Product>("product-1");
product.Price = 99.99m;

// Store with explicit Etag - overrides the one captured during Load
session.Store("product-1", product, etag);
session.SaveChanges();  // Uses the explicitly provided Etag
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L53-L66' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_ManualEtagManagement' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Etag parameter behavior:**
- **`etag = Guid`**: Must match the database Etag or `ConcurrencyException` is thrown
- **`etag = null`**: No concurrency check, last-write-wins

Note that storing with an etag, null or Guid, will always result in an update, never an insert. 

### New Documents (INSERT)

When storing a new document, no Etag check is performed:

<!-- snippet: Concurrency_NewDocuments -->
<a id='snippet-Concurrency_NewDocuments'></a>

```cs
using var session = store.OpenSession();

// Store new document - marked as Transient (will INSERT)
session.Store(new Product { Id = "product-1", Name = "Widget" });

// SaveChanges() performs INSERT - no Etag check needed
session.SaveChanges();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L72-L80' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_NewDocuments' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Note that `lastWriteWins = true` will still fail, if documents with same Id, are inserted concurrently. 

## Transaction Behavior and Concurrency

### Default: Per-Operation Transactions

By default, each operation (Load, SaveChanges) runs in its own short-lived transaction:

<!-- snippet: Concurrency_PerOperationTransactions -->
<a id='snippet-Concurrency_PerOperationTransactions'></a>

```cs
using var session = store.OpenSession();

var product = session.Load<Product>("p1");  // Transaction 1: Read at time T1
// ... time passes, other processes may modify the document ...
product.Price = 99.99m;
session.SaveChanges();                       // Transaction 2: Write at time T2, checks Etag from T1
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L86-L93' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_PerOperationTransactions' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Key point**: Even though Load and SaveChanges run in separate transactions, HybridDb **still checks the Etag** captured during Load. If another process modified the document between T1 and T2, you'll get a `ConcurrencyException`.

**This provides optimistic concurrency without long-running transactions**, giving you:
- High performance (no locks held between operations)
- Concurrency protection (conflicts are detected on write)
- Retry capability (you can catch `ConcurrencyException` and retry)

### Important Caveat: Cross-Document Consistency

**The trade-off**: When loading multiple documents without a transaction, each Load runs in its own transaction. This means documents loaded at different times may not be consistent with each other.

**Example scenario:**
1. Documents A and B have mutual references (e.g., Order references Product, Product tracks OrderCount)
2. Process X loads document A
3. Process Y modifies both A and B, then saves them
4. Process X loads document B
5. Result: Process X now has an inconsistent view—A is old, B is new

**When this matters:**
- Making decisions based on relationships between documents
- Calculating aggregates across documents
- Validating business rules that span multiple documents

**Mitigation strategies:**

1. **Design for single-document decisions** (recommended):
   - Store all information needed for a decision within the document being modified
   - Avoid logic that depends on relationships between documents
   - Use eventual consistency patterns

2. **Verify CommitIds match** (when documents are related):
   <!-- snippet: Concurrency_VerifyCommitIds -->
   <a id='snippet-Concurrency_VerifyCommitIds'></a>
   ```cs
   using var session = store.OpenSession();
   
   var order = session.Load<Order>("order-1");
   var product = session.Load<Product>(order.ProductId);
   
   var orderEtag = session.Advanced.GetEtagFor(order);
   var productEtag = session.Advanced.GetEtagFor(product);
   
   if (orderEtag != productEtag)
   {
       // Product changed since order was created - handle appropriately
   }
   ```
   <sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L99-L112' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_VerifyCommitIds' title='Start of snippet'>anchor</a></sup>
   <!-- endSnippet -->

3. **Use DocumentTransaction** (when you need guaranteed consistency):
   - See next section for details
   - Provides consistent snapshot across all loads
   - Trade-off: higher locking, lower concurrency

**Note**: This consistency challenge exists in any distributed system - whether coordinating between database documents, microservices, or external APIs.

However, when loading **different documents**, each Load still runs in its own transaction and may see different snapshots, as described in the Cross-Document Consistency section above.

### With DocumentTransaction: Session-Lifetime Transaction

When you provide a DocumentTransaction, all operations share the same transaction with consistent isolation:

<!-- snippet: Concurrency_DocumentTransaction -->
<a id='snippet-Concurrency_DocumentTransaction'></a>

```cs
using var tx = store.BeginTransaction(IsolationLevel.RepeatableRead);
using var session = store.OpenSession(tx);

var product = session.Load<Product>("p1");  // Uses tx, acquires locks based on isolation level
product.Price = 99.99m;
session.SaveChanges();                       // Uses same tx, Etag check still performed
tx.Complete();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L118-L126' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_DocumentTransaction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Benefits:**
- Consistent snapshot across all reads in the session
- Prevents phantom reads (depending on isolation level)
- Can coordinate with other SQL operations in same transaction

**Trade-offs:**
- Holds locks longer (from first Load to Complete)
- Can reduce concurrency and increase lock contention
- May impact performance under high load

## Advantages of Optimistic Concurrency

### High Performance
- **No locks during reads**: Load operations don't block other processes
- **No lock waits**: Multiple processes can read and work with documents concurrently
- **Short transactions**: Per-operation transactions minimize lock duration
- **High concurrency**: Scales well with many concurrent users

### Automatic Conflict Detection
- **Built-in Etag checking**: No need to manually manage versioning
- **Clear failure mode**: `ConcurrencyException` tells you exactly what happened
- **Easy retry logic**: Simple to implement exponential backoff retry patterns

### Suitable for Most Scenarios
- **Low contention**: When different users typically work on different documents
- **Read-heavy workloads**: Reads are fast and never block each other
- **Document databases**: Each document is an isolated unit of change

## When Optimistic Concurrency Is Not Enough

Optimistic concurrency (automatic Etag checking) handles most scenarios well, but there are cases where you need stronger guarantees or different behavior.

**Use explicit DocumentTransaction when:**
- You need to coordinate across **multiple sessions**
- You need to integrate with **other SQL operations** (stored procedures, raw SQL)
- You need a **consistent read snapshot** before deciding what to write
- You need to handle many concurrent writes without drowning i retries

### Example

**Problem**: When you need to read values, calculate based on them, then write back, optimistic concurrency provides protection but may cause frequent retries under contention:

<!-- snippet: Concurrency_ReadCalculateWrite -->
<a id='snippet-Concurrency_ReadCalculateWrite'></a>

```cs
using var session = store.OpenSession();

var product = session.Load<Product>("p1");  
var inventory = session.Load<Inventory>("inv-1");

// Calculate based on both
if (inventory.Stock >= product.Stock)
{
    inventory.Stock -= product.Stock;
    // SaveChanges() will check Etags - but if another process modified inventory,
    // we'll get ConcurrencyException and need to retry
    session.SaveChanges();
}
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L132-L146' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_ReadCalculateWrite' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**This pattern works with optimistic concurrency** - the Etag check ensures consistency. However, under high contention, you may experience many retries.

**Alternative with transaction** (for high-contention scenarios):

<!-- snippet: Concurrency_ReadCalculateWriteWithTransaction -->
<a id='snippet-Concurrency_ReadCalculateWriteWithTransaction'></a>

```cs
using var tx = store.BeginTransaction(IsolationLevel.Serializable);
using var session = store.OpenSession(tx);

var product = session.Load<Product>("p1");  // Acquires read lock
var inventory = session.Load<Inventory>("inv-1");  // Acquires read lock

if (inventory.Stock >= product.Stock)
{
    inventory.Stock -= product.Stock;
    session.SaveChanges();  // Etag check still performed, plus transaction guarantees
}

tx.Complete();  // No other process could have modified these documents during the transaction
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L152-L166' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_ReadCalculateWriteWithTransaction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Session-Level Caching

HybridDb sessions cache loaded entities, so if you load the same document twice in the same session, you'll get the same instance from the cache (at the same snapshot). To prevent this you must explicitly evict/reload the same document.

This means that within a single session, you automatically get a consistent view of a document across multiple loads without needing an explicit transaction:

<!-- snippet: Concurrency_SessionCaching -->
<a id='snippet-Concurrency_SessionCaching'></a>

```cs
using var session = store.OpenSession();

var product = session.Load<Product>("p1");  
Console.WriteLine(product.Price);  // 100

// Another process updates product.Price to 150

var productAgain = session.Load<Product>("p1");  
Console.WriteLine(productAgain.Price);  // Still 100 (from session cache)
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc06_ConcurrencyModelTests.cs#L172-L182' title='Snippet source file'>snippet source</a> | <a href='#snippet-Concurrency_SessionCaching' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
