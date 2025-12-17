# DocumentStore and DocumentTransaction

## Overview

The `DocumentStore` is the central class in HybridDb - a long-lived object that manages configuration, database schema, and provides access to sessions and transactions. The `DocumentTransaction` handles Sql Server transactions for either single commmands, a single session or across multiple session.

## DocumentStore

### Role and Lifecycle

The `DocumentStore`:
- **Holds configuration**
- **Manages migrations**
- **Provides access to instances of `DocumentSession`**
- **Provides access to instances of `DocumentTransaction`**
- **Lives application-wide**

**Typical lifecycle**:
1. Create and configure at application startup
2. Initialize database schema (automatic by default)
3. Use throughout application lifetime via `OpenSession()`
4. Dispose on application shutdown



### Store Properties

```csharp
// Configuration access
Configuration config = store.Configuration;

// Database interface (for advanced scenarios)
IDatabase database = store.Database;

// Usage statistics
StoreStats stats = store.Stats;
Console.WriteLine($"Requests: {stats.NumberOfRequests}");
Console.WriteLine($"Loaded: {stats.NumberOfLoadedDocuments}");

// Table mode
TableMode mode = store.TableMode;  // RealTables or GlobalTempTables

// Initialization state
bool ready = store.IsInitialized;
```

## DocumentTransaction

### Understanding Transactions in HybridDb

**All operations in HybridDb run in a transaction** - there is no such thing as a non-transactional operation.

**By default**, each session operation (`Load()`, `SaveChanges()`, etc.) creates its own short-lived transaction:
```csharp
using var session = store.OpenSession();

var product = session.Load<Product>("p1");  // Transaction 1
product.Price = 99.99m;
session.SaveChanges();                       // Transaction 2
```

**When you provide a `DocumentTransaction`** to `OpenSession()`, all operations in that session use the same transaction:
```csharp
using var tx = store.BeginTransaction();
using var session = store.OpenSession(tx);

var product = session.Load<Product>("p1");  // Uses tx
product.Price = 99.99m;
session.SaveChanges();                       // Uses tx

tx.Complete();  // Commits the SaveChanges operations
```

### Why Use DocumentTransaction?

Providing a `DocumentTransaction` serves following key purposes:

**1. Session Consistency**: All loads within the session see a consistent snapshot (according the isolation level). Be aware that this is not the default by design, as it can result in more locking than actually needed for most sessions. HybridDb uses optimistic concurrency checks for writes and often this is adequate and keeps performance up. See the [Concurrency Model](DocumentSession-Concurrency-Model.md) documentation for details.

**2. Cross-Session/Cross-Operation Consistency**: Coordinate changes across multiple sessions and integrate HybridDb operations with other SQL operations. 

**3.  Use specific isolation levels**: And other more advanced transaction options.

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
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L61-L76' title='Snippet source file'>snippet source</a> | <a href='#snippet-BasicTransaction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Key points**:
- Create with `store.BeginTransaction()`
- Pass to `OpenSession(tx)` - all operations in this session now use this transaction
- Multiple sessions can share the same transaction
- Call `tx.Complete()` to commit all operations across all sessions
- Transaction rolls back automatically if not completed or exception occurs

### Transaction Options

#### With Isolation Level

<!-- snippet: TransactionWithIsolationLevel -->
<a id='snippet-TransactionWithIsolationLevel'></a>

```cs
using var tx = store.BeginTransaction(IsolationLevel.Snapshot);

using var session = store.OpenSession(tx);

// both loaded in the same snapshot time
var product1 = session.Load<Product>("product-1"); 
var product2 = session.Load<Product>("product-2");

tx.Complete();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L91-L101' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransactionWithIsolationLevel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

See [SQL Server Transaction Isolation Levels](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-transaction-isolation-level-transact-sql) for details on available isolation levels.

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
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L109-L124' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransactionWithCommitId' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**About CommitId:**
- Every `SaveChanges()` operation gets a `CommitId` (auto-generated if not provided)
- The `CommitId` becomes the `Etag` for all documents modified in that operation
- `SaveChanges()` returns the `CommitId` used
- Used to relate operations together (e.g., correlate a message ID with resulting database changes)
- Can be set via `DocumentTransaction` (as shown above)

```csharp
using var session = store.OpenSession();

// Enqueue a message using session's CommitId as part of the message ID
// If this code is called multiple times in the same session (e.g., from event handlers),
// only one message with this ID will be enqueued (idempotent within the session)
session.Enqueue("my-message/" + session.CommitId, new Message { Payload = "..." });

var commitId = session.SaveChanges();  // Returns the session's CommitId
```

#### With Connection Timeout

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
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L132-L145' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransactionWithConnectionTimeout' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Transaction Properties

The transaction and session share a `CommitId`. This relationship can work in three ways:

**1. Transaction provides CommitId to Session**:
```csharp
var commitId = Guid.NewGuid();
using var tx = store.BeginTransaction(commitId);
using var session = store.OpenSession(tx);  // session.CommitId == commitId
```

**2. Session provides CommitId to Transaction** (enlist after opening):
```csharp
using var session = store.OpenSession();  // session.CommitId auto-generated

using var tx = store.BeginTransaction(session.CommitId);
session.Advanced.Enlist(tx);  // transaction must have same CommitId
```

**3. Session auto-enlists on SaveChanges()** (most common for single operations):
```csharp
using var session = store.OpenSession();  // session.CommitId auto-generated

// On SaveChanges(), session automatically creates and enlists in a new transaction
// with the session's CommitId, executes commands, and completes the transaction
var commitId = session.SaveChanges();  // Returns session.CommitId
```

**Other properties:**
```csharp
// Store reference
IDocumentStore store = tx.Store;

// SQL connection and transaction (for advanced scenarios)
SqlConnection connection = tx.SqlConnection;
SqlTransaction sqlTx = tx.SqlTransaction;
```

### Executing Commands and Queries

The `DocumentTransaction` provides methods for executing custom SQL commands and queries directly against the database. This is useful when you need to integrate HybridDb operations with custom SQL logic, perform bulk operations, or execute queries that go beyond what DocumentSession provides.

#### Execute Method

The `Execute<T>()` method executes a `HybridDbCommand<T>` and returns a result of type `T`. This is the extensibility point for custom database operations.

**Using built-in commands:**

```csharp
using var tx = store.BeginTransaction();

var table = store.Configuration.GetDesignFor<Product>().Table;

// Get documents by IDs
var rows = tx.Execute(new GetCommand(table, new[] { "p1", "p2" }));

// Execute custom SQL
var sql = new SqlBuilder()
    .Append("update Products set Price = Price * 1.1 where CategoryId = @categoryId", 
            new SqlParameter("categoryId", "electronics"));
var commitId = tx.Execute(new SqlCommand(sql, expectedRowCount: 0));

tx.Complete();
```

**Built-in commands:**
- `GetCommand` - Retrieve documents by IDs
- `InsertCommand` - Insert a document
- `UpdateCommand` - Update a document
- `DeleteCommand` - Delete a document
- `ExistsCommand` - Check if a document exists
- `SqlCommand` - Execute custom SQL with expected row count validation

**Custom commands:**

You can create custom commands by implementing `HybridDbCommand<T>` and registering a handler in your store configuration:

```csharp
// Define custom command
public class ArchiveOldOrdersCommand : HybridDbCommand<int>
{
    public DateTime CutoffDate { get; init; }
    
    public static int Execute(DocumentTransaction tx, ArchiveOldOrdersCommand command)
    {
        var sql = new SqlBuilder()
            .Append("update Orders set Archived = 1")
            .Append("where OrderDate < @cutoff", new SqlParameter("cutoff", command.CutoffDate));
        
        // Execute and return number of affected rows
        return tx.SqlConnection.Execute(sql.ToString(), sql.Parameters, tx.SqlTransaction);
    }
}

// Register handler in configuration
var store = DocumentStore.Create(config =>
{
    config.Document<Order>();
    
    // Register custom command handler
    config.Decorate<HybridDbCommandExecutor>((container, inner) => (tx, command) =>
        command is ArchiveOldOrdersCommand archiveCommand
            ? ArchiveOldOrdersCommand.Execute(tx, archiveCommand)
            : inner(tx, command));
});

// Use the custom command
using var tx = store.BeginTransaction();
var archivedCount = tx.Execute(new ArchiveOldOrdersCommand 
{ 
    CutoffDate = DateTime.Now.AddYears(-1) 
});
tx.Complete();
```

#### Query Methods

The transaction provides two `Query<T>()` methods for executing custom SQL queries:

**1. Query with SqlBuilder** - Execute arbitrary SQL queries:

```csharp
using var tx = store.BeginTransaction();

var sql = new SqlBuilder()
    .Append("select * from Products")
    .Append("where Price > @minPrice", new SqlParameter("minPrice", 100));

var products = tx.Query<Product>(sql);

tx.Complete();
```

**2. Query with HybridDb query syntax** - Query document tables with advanced options:

```csharp
using var tx = store.BeginTransaction();
using var session = store.OpenSession(tx);

var table = store.Configuration.GetDesignFor<Product>().Table;

// Complex query with window, ordering, and filtering
var (stats, results) = tx.Query<Product>(
    table: table,
    join: "inner join Categories c on Products.CategoryId = c.Id",
    where: "Price > @minPrice and c.Name = @category",
    orderby: "Price desc",
    window: new SkipTake(skip: 0, take: 10),
    parameters: new { minPrice = 100, category = "Electronics" }
);

foreach (var result in results)
{
    Console.WriteLine($"{result.Document.Name}: ${result.Document.Price}");
}

tx.Complete();
```

**Query parameters:**
- `table`: The DocumentTable to query from
- `join`: SQL JOIN clause for joining with other tables
- `select`: Custom SELECT clause (defaults to all columns)
- `where`: SQL WHERE condition
- `orderby`: SQL ORDER BY clause
- `window`: Pagination window (`SkipTake` or `SkipToId`)
- `top1`: Return only first result
- `includeDeleted`: Include soft-deleted documents
- `parameters`: Query parameters as anonymous object

### Relating Operations with CommitId

Use CommitId to correlate external events with database changes:

<!-- snippet: IdempotentOperations -->
<a id='snippet-IdempotentOperations'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc07_TransactionTests.cs#L237-L253' title='Snippet source file'>snippet source</a> | <a href='#snippet-IdempotentOperations' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This allows you to:
- Track which database changes came from which external event/message
- Query documents by their CommitId (Etag) to find all changes from a specific operation
- Correlate database state with external system events for debugging and auditing