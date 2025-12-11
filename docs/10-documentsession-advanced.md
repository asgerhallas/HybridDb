# DocumentSession - Advanced Scenarios

## Session Management

### Evicting Entities

Remove an entity from session tracking:

```csharp
using (var session = store.OpenSession())
{
    var product = session.Load<Product>("product-1");
    
    // Evict from session cache
    session.Advanced.Evict(product);
    
    // Loading again will hit the database
    var product2 = session.Load<Product>("product-1");
    
    // Different instances (not cached)
    Assert.NotSame(product, product2);
}
```

### Clearing the Session

Clear all tracked entities and deferred commands:

```csharp
using (var session = store.OpenSession())
{
    session.Store(new Product { Id = "p1", Name = "Widget" });
    session.Store(new Product { Id = "p2", Name = "Gadget" });
    
    // Clear everything
    session.Advanced.Clear();
    
    // Session is now empty
    session.SaveChanges();  // Nothing saved
}
```

### Copying a Session

Create a copy of a session with the same state:

```csharp
using (var session1 = store.OpenSession())
{
    session1.Store(new Product { Id = "p1", Name = "Widget" });
    
    // Create a copy
    using (var session2 = session1.Advanced.Copy())
    {
        // session2 has the same tracked entities
        var product = session2.Load<Product>("p1");
        Assert.Equal("Widget", product.Name);
    }
}
```

## Entity Metadata

### Getting Metadata

Retrieve metadata for an entity:

```csharp
using (var session = store.OpenSession())
{
    var product = session.Load<Product>("product-1");
    
    var metadata = session.Advanced.GetMetadataFor(product);
    
    if (metadata.TryGetValue("CreatedBy", out var createdByList))
    {
        var createdBy = createdByList.FirstOrDefault();
        Console.WriteLine($"Created by: {createdBy}");
    }
}
```

### Setting Metadata

Add or update metadata for an entity:

```csharp
using (var session = store.OpenSession())
{
    var product = new Product { Id = "p1", Name = "Widget" };
    session.Store(product);
    
    var metadata = new Dictionary<string, List<string>>
    {
        ["CreatedBy"] = new List<string> { "john@example.com" },
        ["Tags"] = new List<string> { "electronics", "gadgets" },
        ["Version"] = new List<string> { "1.0" }
    };
    
    session.Advanced.SetMetadataFor(product, metadata);
    session.SaveChanges();
}
```

### Metadata Use Cases

```csharp
// Audit trail
var metadata = new Dictionary<string, List<string>>
{
    ["CreatedBy"] = new List<string> { currentUser.Email },
    ["CreatedAt"] = new List<string> { DateTime.UtcNow.ToString("o") },
    ["ModifiedBy"] = new List<string> { currentUser.Email },
    ["ModifiedAt"] = new List<string> { DateTime.UtcNow.ToString("o") },
    ["IPAddress"] = new List<string> { httpContext.Connection.RemoteIpAddress.ToString() }
};

session.Advanced.SetMetadataFor(entity, metadata);
```

## ETags and Optimistic Concurrency

### Getting ETags

Retrieve the ETag for an entity:

```csharp
using (var session = store.OpenSession())
{
    var product = session.Load<Product>("product-1");
    var etag = session.Advanced.GetEtagFor(product);
    
    Console.WriteLine($"Current ETag: {etag}");
}
```

### Using ETags for Concurrency Control

```csharp
public void UpdateProductPrice(string productId, decimal newPrice)
{
    using (var session = store.OpenSession())
    {
        var product = session.Load<Product>(productId);
        var etag = session.Advanced.GetEtagFor(product);
        
        product.Price = newPrice;
        
        // Store with ETag check
        session.Store(productId, product, etag);
        
        try
        {
            session.SaveChanges();
        }
        catch (ConcurrencyException)
        {
            // Another process modified the document
            throw new InvalidOperationException("Product was modified by another user");
        }
    }
}
```

### Retry Pattern with ETags

```csharp
public async Task<bool> TryUpdateWithRetry<T>(
    string id, 
    Action<T> update, 
    int maxRetries = 3) where T : class
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            using (var session = store.OpenSession())
            {
                var entity = session.Load<T>(id);
                if (entity == null) return false;
                
                var etag = session.Advanced.GetEtagFor(entity);
                
                update(entity);
                
                session.Store(id, entity, etag);
                session.SaveChanges();
                
                return true;
            }
        }
        catch (ConcurrencyException) when (attempt < maxRetries - 1)
        {
            // Exponential backoff
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100));
        }
    }
    
    return false;
}

// Usage
await TryUpdateWithRetry<Product>("product-1", product => 
{
    product.Stock--;
});
```

## Checking Existence

### Check if Document Exists

```csharp
using (var session = store.OpenSession())
{
    // Check existence without loading
    bool exists = session.Advanced.Exists<Product>("product-1", out Guid? etag);
    
    if (exists)
    {
        Console.WriteLine($"Document exists with ETag: {etag}");
    }
}
```

### Exists vs Load

```csharp
// Good: Use Exists to check without loading full document
if (session.Advanced.Exists<Product>("product-1", out _))
{
    // Do something
}

// Less efficient: Load to check existence
var product = session.Load<Product>("product-1");
if (product != null)
{
    // Do something
}
```

## Deferred Commands

Execute custom commands when `SaveChanges` is called:

### Basic Deferred Command

```csharp
using (var session = store.OpenSession())
{
    var table = store.Configuration.GetDesignFor<Product>().Table;
    
    // Defer a custom insert
    session.Advanced.Defer(new InsertCommand(
        table,
        "product-custom",
        new Dictionary<string, object>
        {
            ["Id"] = "product-custom",
            ["Document"] = "{\"Name\":\"Custom Product\"}"
        }
    ));
    
    session.SaveChanges();  // Command executes here
}
```

### Batch Operations with Deferred Commands

```csharp
using (var session = store.OpenSession())
{
    var table = store.Configuration.GetDesignFor<Product>().Table;
    
    // Defer multiple commands
    for (int i = 0; i < 100; i++)
    {
        session.Advanced.Defer(new InsertCommand(
            table,
            $"product-{i}",
            CreateProjections(new Product { Id = $"product-{i}", Name = $"Product {i}" })
        ));
    }
    
    // All executed in one transaction
    session.SaveChanges();
}
```

### Custom SQL Commands

```csharp
using (var session = store.OpenSession())
{
    // Defer a raw SQL command
    session.Advanced.Defer(new SqlCommand(
        new SqlBuilder()
            .Append("UPDATE Products SET Featured = 1")
            .Append("WHERE CategoryId = @category", new SqlParameter("category", "electronics"))
    ));
    
    session.SaveChanges();
}
```

## Managed Entities

### Accessing Managed Entities

```csharp
using (var session = store.OpenSession())
{
    var product = session.Load<Product>("product-1");
    
    // Get managed entity information
    var managedEntities = session.Advanced.ManagedEntities;
    
    foreach (var managed in managedEntities.Values)
    {
        Console.WriteLine($"Key: {managed.Key}");
        Console.WriteLine($"State: {managed.State}");
        Console.WriteLine($"Type: {managed.Design.DocumentType}");
    }
}
```

### Checking if Entity is Managed

```csharp
using (var session = store.OpenSession())
{
    var product = new Product { Id = "p1", Name = "Widget" };
    
    // Not yet managed
    bool isManaged = session.Advanced.TryGetManagedEntity<Product>("p1", out _);
    Assert.False(isManaged);
    
    // Store it
    session.Store(product);
    
    // Now managed
    isManaged = session.Advanced.TryGetManagedEntity<Product>("p1", out var managedProduct);
    Assert.True(isManaged);
    Assert.Same(product, managedProduct);
}
```

## Session Data

Store arbitrary data in the session:

```csharp
using (var session = store.OpenSession())
{
    // Store data in session
    session.Advanced.SessionData["CurrentUser"] = "john@example.com";
    session.Advanced.SessionData["RequestId"] = Guid.NewGuid();
    
    // Retrieve later
    var userId = (string)session.Advanced.SessionData["CurrentUser"];
    
    ProcessOrder(session);
}

void ProcessOrder(IDocumentSession session)
{
    // Access session data
    var userId = (string)session.Advanced.SessionData["CurrentUser"];
    
    // Use in business logic
    var order = new Order 
    { 
        Id = Guid.NewGuid().ToString(),
        CreatedBy = userId
    };
    
    session.Store(order);
    session.SaveChanges();
}
```

## Transaction Enlistment

### Enlisting in a Transaction

```csharp
using (var tx = store.BeginTransaction())
{
    using (var session = store.OpenSession())
    {
        // Enlist session in transaction
        session.Advanced.Enlist(tx);
        
        session.Store(new Product { Id = "p1", Name = "Widget" });
        session.SaveChanges();
    }
    
    tx.Complete();
}
```

### Multiple Sessions in One Transaction

```csharp
using (var tx = store.BeginTransaction())
{
    // Session 1
    using (var session1 = store.OpenSession())
    {
        session1.Advanced.Enlist(tx);
        session1.Store(new Product { Id = "p1", Name = "Widget" });
        session1.SaveChanges();
    }
    
    // Session 2
    using (var session2 = store.OpenSession())
    {
        session2.Advanced.Enlist(tx);
        session2.Store(new Order { Id = "o1", ProductId = "p1" });
        session2.SaveChanges();
    }
    
    // Both committed together
    tx.Complete();
}
```

## Accessing Store and Transaction

```csharp
using (var session = store.OpenSession())
{
    // Access the document store
    var documentStore = session.Advanced.DocumentStore;
    
    // Access current transaction (if any)
    var transaction = session.Advanced.DocumentTransaction;
    
    if (transaction != null)
    {
        Console.WriteLine($"Commit ID: {transaction.CommitId}");
    }
}
```

## Advanced Query Scenarios

### Custom Projections

```csharp
using (var session = store.OpenSession())
{
    var sql = new SqlBuilder()
        .Append(@"
            SELECT 
                p.Id,
                p.Name,
                p.Price,
                c.Name as CategoryName
            FROM Products p
            LEFT JOIN Categories c ON p.CategoryId = c.Id
            WHERE p.Price > @minPrice",
            new SqlParameter("minPrice", 100)
        );
    
    var results = session.Query<dynamic>(sql).ToList();
    
    foreach (var result in results)
    {
        Console.WriteLine($"{result.Name} - {result.CategoryName}: ${result.Price}");
    }
}
```

### Aggregations

```csharp
using (var session = store.OpenSession())
{
    var sql = new SqlBuilder()
        .Append(@"
            SELECT 
                CategoryId,
                COUNT(*) as ProductCount,
                AVG(Price) as AvgPrice,
                MAX(Price) as MaxPrice
            FROM Products
            GROUP BY CategoryId
        ");
    
    var stats = session.Query<dynamic>(sql).ToList();
}
```

## Event Handling

Listen to session events via store configuration:

```csharp
store.Configuration.AddEventHandler(@event =>
{
    switch (@event)
    {
        case SaveChanges_BeforePrepareCommands before:
            Console.WriteLine($"About to save {before.Session.Advanced.ManagedEntities.Count} entities");
            break;
            
        case SaveChanges_AfterExecuteCommands after:
            Console.WriteLine($"Executed {after.ExecutedCommands.Count} commands");
            break;
            
        case EntityLoaded loaded:
            Console.WriteLine($"Loaded {loaded.ManagedEntity.Design.DocumentType.Name}");
            break;
    }
});
```

## Best Practices

### 1. Use Evict for Large Operations

```csharp
// Process large batch without keeping all in memory
using (var session = store.OpenSession())
{
    foreach (var id in largeListOfIds)
    {
        var product = session.Load<Product>(id);
        UpdateProduct(product);
        session.SaveChanges();
        
        // Evict to free memory
        session.Advanced.Evict(product);
    }
}
```

### 2. Use Metadata for Audit Trails

```csharp
public void StoreWithAudit<T>(IDocumentSession session, T entity, string userId) where T : class
{
    session.Store(entity);
    
    var metadata = new Dictionary<string, List<string>>
    {
        ["ModifiedBy"] = new List<string> { userId },
        ["ModifiedAt"] = new List<string> { DateTime.UtcNow.ToString("o") }
    };
    
    session.Advanced.SetMetadataFor(entity, metadata);
}
```

### 3. Check Existence Before Load

```csharp
// Efficient: Check first
if (session.Advanced.Exists<Product>(productId, out var etag))
{
    // Only load if exists
    var product = session.Load<Product>(productId);
}

// Less efficient: Load and check
var product = session.Load<Product>(productId);
if (product != null)
{
    // ...
}
```

### 4. Use Session Data for Context

```csharp
public class AuditInterceptor
{
    public void OnSave(IDocumentSession session, object entity)
    {
        if (session.Advanced.SessionData.TryGetValue("UserId", out var userId))
        {
            var metadata = session.Advanced.GetMetadataFor(entity) 
                ?? new Dictionary<string, List<string>>();
            
            metadata["ModifiedBy"] = new List<string> { userId.ToString() };
            session.Advanced.SetMetadataFor(entity, metadata);
        }
    }
}
```

### 5. Implement Optimistic Concurrency Strategically

```csharp
// For critical operations
public void DecrementStock(string productId, int quantity)
{
    using (var session = store.OpenSession())
    {
        var product = session.Load<Product>(productId);
        var etag = session.Advanced.GetEtagFor(product);
        
        if (product.Stock < quantity)
        {
            throw new InvalidOperationException("Insufficient stock");
        }
        
        product.Stock -= quantity;
        
        session.Store(productId, product, etag);  // Ensure no concurrent updates
        session.SaveChanges();
    }
}

// For non-critical operations, last write wins
public void UpdateDescription(string productId, string description)
{
    using (var session = store.OpenSession())
    {
        var product = session.Load<Product>(productId);
        product.Description = description;
        
        session.SaveChanges(lastWriteWins: true);  // Allow overwrites
    }
}
```

## Troubleshooting

### Session Already Saving

If you get "Session is not in a valid state":
- Don't call `SaveChanges` multiple times in exception handlers
- Don't reuse sessions after errors
- Dispose and create a new session

### Entity Not Tracked

If changes aren't saved:
- Ensure entity was loaded or stored in the session
- Check that entity wasn't evicted
- Verify you're not mixing entities from different sessions

### Memory Issues

For large operations:
- Use `Evict` to release entities from cache
- Consider batching with multiple sessions
- Use read-only when not modifying
- Clear session periodically

### Deferred Commands Not Executing

Ensure:
- `SaveChanges()` is called
- Commands are valid
- No exceptions thrown before save
