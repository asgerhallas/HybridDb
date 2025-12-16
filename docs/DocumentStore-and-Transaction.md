# The DocumentStore and DocumentTransaction

## DocumentStore

The `DocumentStore` is the central component of HybridDb. It manages database connections, configuration, schema, and provides access to sessions and transactions.

### Creating a DocumentStore

#### For Production

<!-- snippet: CreateDocumentStore_ForProduction -->
<a id='snippet-CreateDocumentStore_ForProduction'></a>

```cs
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
}, initialize: false);
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L22-L37' title='Snippet source file'>snippet source</a> | <a href='#snippet-CreateDocumentStore_ForProduction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### For Testing

<!-- snippet: CreateDocumentStore_ForTesting -->
<a id='snippet-CreateDocumentStore_ForTesting'></a>

```cs
var newStore = DocumentStore.ForTesting(
    TableMode.GlobalTempTables,
    config =>
    {
        config.Document<Product>()
            .With(x => x.Name);
    }
);
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L45-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-CreateDocumentStore_ForTesting' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### DocumentStore Properties

```csharp
// Configuration object
Configuration configuration = store.Configuration;

// Database interface
IDatabase database = store.Database;

// Statistics
StoreStats stats = store.Stats;
Console.WriteLine($"Requests: {stats.NumberOfRequests}");
Console.WriteLine($"Commands: {stats.NumberOfCommands}");

// Table mode (RealTables or GlobalTempTables)
TableMode tableMode = store.TableMode;

// Initialization state
bool isInitialized = store.IsInitialized;
```

### Opening Sessions

Sessions represent a unit of work:

```csharp
using var session = store.OpenSession();

var product = session.Load<Product>("product-1");
product.Price = 99.99m;
session.SaveChanges();
```

### Executing Commands

Direct command execution:

```csharp
// Execute a command
var table = store.Configuration.GetDesignFor<Product>().Table;
var result = store.Execute(
transaction, 
new GetCommand(table, new[] { "product-1" })
);

// Execute a command with result
var exists = store.Execute(
transaction,
new ExistsCommand(table, "product-1")
);
```

### Store Lifecycle

```csharp
// Create store (automatically initializes by default)
var store = DocumentStore.Create(config => { /* ... */ });

// Use the store
using var session = store.OpenSession();

// ...

// Dispose when done (important for temp tables)
store.Dispose();
```

**Manual Initialization**: If you need to configure the store from multiple places before initializing:

```csharp
// Disable automatic initialization
var store = DocumentStore.Create(config => { /* ... */ }, initialize: false);

// Configure from multiple places
store.Configuration.Document<Product>().With(x => x.Name);
store.Configuration.Document<Order>().With(x => x.OrderDate);

// Manually initialize when ready
store.Initialize();
```

### Store Statistics

Track usage with built-in statistics:

```csharp
Console.WriteLine($"Requests: {store.Stats.NumberOfRequests}");
Console.WriteLine($"Queries: {store.Stats.NumberOfQueries}");
Console.WriteLine($"Commands: {store.Stats.NumberOfCommands}");
Console.WriteLine($"Loaded: {store.Stats.NumberOfLoadedDocuments}");
Console.WriteLine($"Saved: {store.Stats.NumberOfSavedDocuments}");
```

### Events and Notifications

Subscribe to store events:

```csharp
store.Configuration.AddEventHandler(@event =>
{
switch (@event)
{
        case MigrationStarted started:
            Console.WriteLine("Migration started");
            break;
            
        case MigrationEnded ended:
            Console.WriteLine("Migration completed");
            break;
            
        case SqlCommandExecuted executed:
            Console.WriteLine($"SQL: {executed.Sql}");
            break;
}
});
```

## DocumentTransaction

`DocumentTransaction` provides explicit transaction management for operations that span multiple sessions.

### Creating Transactions

#### Basic Transaction

<!-- snippet: BasicTransaction -->
<a id='snippet-BasicTransaction'></a>

```cs
using var tx = store.BeginTransaction();

using var session = store.OpenSession(tx);

session.Store(new Product { Id = "product-1", Name = "Widget" });
session.SaveChanges();

using var session2 = store.OpenSession(tx);

session2.Store(new Order { Id = "order-1", ProductId = "product-1" });
session2.SaveChanges();

// Commit the transaction
tx.Complete();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L66-L81' title='Snippet source file'>snippet source</a> | <a href='#snippet-BasicTransaction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### Multiple Sessions in One Transaction

A more realistic example showing how to coordinate multiple operations across different sessions:

<!-- snippet: MultiSessionTransaction -->
<a id='snippet-MultiSessionTransaction'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L198-L225' title='Snippet source file'>snippet source</a> | <a href='#snippet-MultiSessionTransaction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### With Isolation Level

<!-- snippet: TransactionWithIsolationLevel -->
<a id='snippet-TransactionWithIsolationLevel'></a>

```cs
using var tx = store.BeginTransaction(IsolationLevel.Serializable);

// Critical section with serializable isolation
using var session = store.OpenSession(tx);

var product = session.Load<Product>("product-1");
product.Stock--;
session.SaveChanges();

tx.Complete();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L96-L107' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransactionWithIsolationLevel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### With Commit ID

<!-- snippet: TransactionWithCommitId -->
<a id='snippet-TransactionWithCommitId'></a>

```cs
var commitId = Guid.NewGuid();

using var tx = store.BeginTransaction(commitId);

// All changes in this transaction will have the same commit ID
// Useful for tracking related changes

using var session = store.OpenSession(tx);

var product = new Product { Id = "p1", Name = "Widget" };
session.Store(product);
session.SaveChanges();

tx.Complete();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L115-L130' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransactionWithCommitId' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Transaction Properties

```csharp
// Commit ID
Guid commitId = tx.CommitId;

// SQL connection and transaction
SqlConnection connection = tx.SqlConnection;
SqlTransaction sqlTransaction = tx.SqlTransaction;

// Store reference
IDocumentStore store = tx.Store;
```

### Transaction Isolation Levels

```csharp
// Read Committed (default)
using var tx = store.BeginTransaction(IsolationLevel.ReadCommitted);

// Read Uncommitted
using var tx = store.BeginTransaction(IsolationLevel.ReadUncommitted);

// Repeatable Read
using var tx = store.BeginTransaction(IsolationLevel.RepeatableRead);

// Serializable (highest isolation, may impact performance)
using var tx = store.BeginTransaction(IsolationLevel.Serializable);

// Snapshot (uses row versioning)
using var tx = store.BeginTransaction(IsolationLevel.Snapshot);
```

### Executing Commands in Transactions

```csharp
 using var tx = store.BeginTransaction();

var table = store.Configuration.GetDesignFor<Product>().Table;

// Get document
var doc = tx.Get(table, "product-1");

// Get multiple documents
var docs = tx.Get(table, new[] { "product-1", "product-2" });

// Query
var (stats, results) = tx.Query<Product>(
table,
join: "",
where: "Price > 100"
);

tx.Complete();
```

### Transaction Timeout

Set a timeout for acquiring the database connection:

<!-- snippet: TransactionWithConnectionTimeout -->
<a id='snippet-TransactionWithConnectionTimeout'></a>

```cs
using (var tx = store.BeginTransaction(
    IsolationLevel.ReadCommitted, 
    connectionTimeout: TimeSpan.FromSeconds(30)))
{
    // Transaction will fail if connection can't be acquired within 30 seconds
    
    using var session = store.OpenSession(tx);

    // ...
    
    tx.Complete();
}
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L138-L151' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransactionWithConnectionTimeout' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Rollback

Transactions automatically roll back if not completed:

<!-- snippet: TransactionRollback -->
<a id='snippet-TransactionRollback'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L160-L182' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransactionRollback' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Distributed Transaction

Use with TransactionScope for distributed transactions:

```csharp
using (var scope = new TransactionScope())
{
    // HybridDb transaction
    using (var session = store.OpenSession())
    {
        session.Store(new Product { Id = "product-1" });
        session.SaveChanges();
    }

    // Another database operation
    using (var otherConnection = new SqlConnection(otherConnectionString))
    {
        otherConnection.Open();
        // ...
    }

    scope.Complete();
}
```

### Optimistic Concurrency

Use ETags for optimistic locking:

```csharp
using var session = store.OpenSession();

var product = session.Load<Product>("product-1");
var etag = session.Advanced.GetEtagFor(product);

// ... do work ...

product.Price = 99.99m;

// Store with the original etag
session.Store("product-1", product, etag);

try
{
session.SaveChanges();
}
catch (ConcurrencyException)
{
// Document was modified by another process
// Handle conflict
```

### Idempotent Operations

Use consistent commit IDs for idempotent operations:

<!-- snippet: IdempotentOperations -->
<a id='snippet-IdempotentOperations'></a>

```cs
// Use orderId as commitId for idempotency
using (var tx = store.BeginTransaction(orderId))
{
    using (var session = store.OpenSession(tx))
    {
        // Check if already processed
        if (session.Advanced.Exists<ProcessedOrder>(orderId.ToString(), out _))
        {
            return; // Already processed
        }
        
        // Process order
        var order = session.Load<Order>(orderId.ToString());
        
        // Mark as processed
        session.Store(new ProcessedOrder { Id = orderId.ToString() });
        
        session.SaveChanges();
    }
    
    tx.Complete();
}
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L243-L266' title='Snippet source file'>snippet source</a> | <a href='#snippet-IdempotentOperations' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Best Practices

### 1. Keep Transactions Short

<!-- snippet: ShortTransaction_Good -->
<a id='snippet-ShortTransaction_Good'></a>

```cs
// Good: Quick transaction
using (var tx = store.BeginTransaction())
{
    using var session = store.OpenSession(tx);

    var product = session.Load<Product>("product-1");
    product.Price = 99.99m;
    session.SaveChanges();
    
    tx.Complete();
}
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L281-L293' title='Snippet source file'>snippet source</a> | <a href='#snippet-ShortTransaction_Good' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

// Avoid: Long-running transaction
using (var tx = store.BeginTransaction())
{
    // Don't do expensive I/O or external API calls in transaction
    var data = await DownloadFromExternalApi();  // Bad!

    using var session = store.OpenSession(tx);

    session.Store(ConvertToProduct(data));
    session.SaveChanges();
    
    tx.Complete();
}
```

### 2. Use Appropriate Isolation Levels

```csharp
// For most operations: ReadCommitted (default)
using var tx = store.BeginTransaction();

// For critical inventory: Serializable
using var tx = store.BeginTransaction(IsolationLevel.Serializable);

// For read-heavy operations: Snapshot
using var tx = store.BeginTransaction(IsolationLevel.Snapshot);
```

### 3. Always Dispose Transactions

```csharp
// Good: Using statement ensures disposal
using (var tx = store.BeginTransaction())
{
// ...
tx.Complete();
}

// Or with explicit disposal
var tx = store.BeginTransaction();
try
{
// ...
tx.Complete();
}
finally
{
tx.Dispose();
}
```

### 4. Handle Concurrency Exceptions

<!-- snippet: ConcurrencyExceptionRetry -->
<a id='snippet-ConcurrencyExceptionRetry'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L309-L332' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConcurrencyExceptionRetry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### 5. Use Transactions for Related Changes

<!-- snippet: RelatedChangesTransaction -->
<a id='snippet-RelatedChangesTransaction'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L349-L365' title='Snippet source file'>snippet source</a> | <a href='#snippet-RelatedChangesTransaction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Troubleshooting

### Transaction Deadlocks

If you encounter deadlocks:
- Keep transactions short
- Access tables in consistent order
- Use lower isolation levels when appropriate
- Consider using Snapshot isolation

### Transaction Timeout

If transactions timeout:
- Reduce transaction duration
- Increase connection timeout
- Check for blocking queries
- Optimize database indexes

### Connection Pool Exhaustion

If you run out of connections:
- Ensure transactions are disposed
- Use `using` statements
- Check for connection leaks
- Increase connection pool size in connection string

### Memory Issues

For large transactions:
- Batch operations
- Don't load too many documents at once
- Use queries with filtering
- Consider pagination
