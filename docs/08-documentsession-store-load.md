# DocumentSession - Store and Load

## Overview

The `DocumentSession` represents a unit of work and acts as a first-level cache for documents. It tracks entities loaded from or stored to the database and batches all changes until `SaveChanges()` is called.

## Creating a Session

### Basic Session

```csharp
using var session = store.OpenSession();

// Work with documents
session.SaveChanges();
```

### Session with Transaction

```csharp
using var tx = store.BeginTransaction();

using var session = store.OpenSession(tx);

// Work with documents
session.SaveChanges();

tx.Complete();
```

## Storing Documents

### Store New Document

Store a new document with auto-generated or specified ID:

```csharp
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
```

### Store with Explicit Key

```csharp
using var session = store.OpenSession();

var product = new Product 
{ 
    Name = "Widget",
    Price = 19.99m
};

// Specify the key explicitly
session.Store("product-123", product);
session.SaveChanges();
```

### Store with Etag (Update)

Store an existing document with optimistic concurrency control:

```csharp
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
```

### Store Multiple Documents

```csharp
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
```

## Loading Documents

### Load Single Document

```csharp
using var session = store.OpenSession();

var product = session.Load<Product>("product-123");

if (product != null)
{
    Console.WriteLine($"Found: {product.Name}");
}
```

### Load Multiple Documents

```csharp
using var session = store.OpenSession();

var ids = new[] { "product-1", "product-2", "product-3" };
var products = session.Load<Product>(ids);

Console.WriteLine($"Loaded {products.Count} products");
```

### Load with Type Checking

```csharp
using var session = store.OpenSession();

// Load specific type
var product = session.Load<Product>("product-123");

// Load base type (polymorphism)
var entity = session.Load<Entity>("some-id");

// Check actual type
if (entity is Product p)
{
    Console.WriteLine($"Product: {p.Name}");
}
```

### Load as Read-Only

Load documents that won't be modified:

```csharp
using var session = store.OpenSession();
    var product = session.Load<Product>("product-123", readOnly: true);
    
    // product can be read but not modified
    Console.WriteLine(product.Name);
    
    // This would throw an exception:
    // session.Store(product);
```

### Load Multiple as Read-Only

```csharp
using var session = store.OpenSession();

var ids = new[] { "p1", "p2", "p3" };
var products = session.Load<Product>(ids, readOnly: true);

foreach (var product in products)
{
    Console.WriteLine(product.Name);
}
```

## SaveChanges

### Basic Save

```csharp
using var session = store.OpenSession();

session.Store(new Product { Id = "p1", Name = "Widget" });

var commitId = session.SaveChanges();
Console.WriteLine($"Saved with commit ID: {commitId}");
```

### Save with Last Write Wins

Ignore concurrency checks:

```csharp
using var session = store.OpenSession();

var product = session.Load<Product>("product-123");
product.Price = 29.99m;

// Save without checking etag
session.SaveChanges(lastWriteWins: true, forceWriteUnchangedDocument: false);
```

### Force Write Unchanged Documents

Write documents even if they haven't changed:

```csharp
using var session = store.OpenSession();`n`n    var product = session.Load<Product>("product-123");
    
    // Force save even if unchanged (updates ModifiedAt timestamp)
    session.SaveChanges(lastWriteWins: false, forceWriteUnchangedDocument: true);
}
```

## Deleting Documents

### Delete by Entity

```csharp
using var session = store.OpenSession();`n`n    var product = session.Load<Product>("product-123");
    
    session.Delete(product);
    session.SaveChanges();
}
```

### Delete Without Loading

```csharp
using var session = store.OpenSession();`n`n    // Create a stub entity to delete
    var product = new Product { Id = "product-123" };
    session.Store(product, Guid.Empty);  // Store with empty etag
    session.Delete(product);
    
    session.SaveChanges();
}
```

### Soft Delete

If soft delete is configured, documents are marked as deleted but not removed:

```csharp
// In configuration
config.UseSoftDelete();

// Deleted documents remain in database
using var session = store.OpenSession();`n`n    var product = session.Load<Product>("product-123");
    session.Delete(product);
    session.SaveChanges();
    
    // Document is marked deleted but still in table
}

// Query including deleted documents
using var session = store.OpenSession();`n`n    var sql = new SqlBuilder()
        .Append("select * from Products where Id = @id", new SqlParameter("id", "product-123"));
    
    var deleted = session.Query<Product>(sql).FirstOrDefault();
    // Can retrieve soft-deleted document
}
```

## Entity States

Documents in a session have different states:

```csharp
public enum EntityState
{
    Transient,  // New document, not yet in database
    Loaded,     // Loaded from database or updated
    Deleted     // Marked for deletion
}
```

### Checking Entity State

```csharp
using var session = store.OpenSession();`n`n    // New entity: Transient
    var newProduct = new Product { Id = "p1", Name = "New" };
    session.Store(newProduct);
    // State: Transient
    
    session.SaveChanges();
    // State: Loaded
    
    // Load existing: Loaded
    var existing = session.Load<Product>("p2");
    // State: Loaded
    
    // Delete: Deleted
    session.Delete(existing);
    // State: Deleted
    
    session.SaveChanges();
}
```

## Session as First-Level Cache

The session caches loaded documents:

```csharp
using var session = store.OpenSession();`n`n    // First load hits database
    var product1 = session.Load<Product>("product-123");
    
    // Second load returns cached instance (no database hit)
    var product2 = session.Load<Product>("product-123");
    
    // Same instance
    Assert.Same(product1, product2);
}
```

### Cache Implications

```csharp
using var session = store.OpenSession();`n`n    var product = session.Load<Product>("product-123");
    product.Price = 99.99m;
    
    // Loading again returns the modified instance
    var sameProduct = session.Load<Product>("product-123");
    Assert.Equal(99.99m, sameProduct.Price);
    
    // Changes not saved yet
    session.SaveChanges();
}
```

## Working with Related Documents

### Loading Related Documents

```csharp
public class Order
{
    public string Id { get; set; }
    public string CustomerId { get; set; }
    public List<string> ProductIds { get; set; }
}

using var session = store.OpenSession();`n`n    var order = session.Load<Order>("order-123");
    
    // Load customer
    var customer = session.Load<Customer>(order.CustomerId);
    
    // Load products
    var products = session.Load<Product>(order.ProductIds);
}
```

### Storing Related Documents

```csharp
using var session = store.OpenSession();`n`n    var customer = new Customer 
    { 
        Id = "customer-1",
        Name = "John Doe"
    };
    
    var order = new Order 
    { 
        Id = "order-1",
        CustomerId = customer.Id,
        Total = 100.00m
    };
    
    session.Store(customer);
    session.Store(order);
    
    session.SaveChanges();
}
```

## Commit ID

Every save operation has a commit ID:

```csharp
using var session = store.OpenSession();`n`n    // Auto-generated commit ID
    var commitId = session.CommitId;
    Console.WriteLine($"This session's commit ID: {commitId}");
    
    session.Store(new Product { Id = "p1", Name = "Widget" });
    
    var savedCommitId = session.SaveChanges();
    Assert.Equal(commitId, savedCommitId);
}
```

### Tracking Changes by Commit ID

```csharp
// Store with specific commit ID via transaction
var commitId = Guid.NewGuid();

 using var tx = store.BeginTransaction(commitId);

using var session = store.OpenSession(tx);`n`n        session.Store(new Product { Id = "p1", Name = "Widget" });
    session.SaveChanges();
    
    using var session = store.OpenSession(tx);`n`n        session.Store(new Order { Id = "o1", ProductId = "p1" });
        session.SaveChanges();
    }
    
    tx.Complete();
}

// Both documents have the same commit ID
```

## Best Practices

### 1. Keep Sessions Short-Lived

```csharp
// Good: Short-lived session
public Product GetProduct(string id)
{
     using var session = store.OpenSession();

    return session.Load<Product>(id);

// Avoid: Long-lived session
public class ProductRepository
{
    private IDocumentSession session;  // Bad!
    
    public ProductRepository(IDocumentStore store)
    {
    session = store.OpenSession();  // Session kept open
```

### 2. Use Read-Only for Read Operations

```csharp
// Good: Read-only for queries
public List<Product> GetProducts(List<string> ids)
{
    using (var session = store.OpenSession())
    {
    return session.Load<Product>(ids, readOnly: true).ToList();
```

### 3. Batch Related Changes

```csharp
// Good: Batch related operations
using var session = store.OpenSession();`n`n    session.Store(customer);
    session.Store(order);
    session.Store(invoice);
    
    session.SaveChanges();  // Single transaction
}

// Avoid: Multiple saves
using var session = store.OpenSession();`n`n    session.Store(customer);
    session.SaveChanges();
}
using var session = store.OpenSession();`n`n    session.Store(order);
    session.SaveChanges();
}
```

### 4. Handle Concurrency Exceptions

```csharp
public void UpdateProductPrice(string id, decimal newPrice)
{
    int retries = 3;
    while (retries-- > 0)
    {
    try
    {
        using (var session = store.OpenSession())
        {
            var product = session.Load<Product>(id);
            var etag = session.Advanced.GetEtagFor(product);
            
            product.Price = newPrice;
            session.Store(id, product, etag);
            session.SaveChanges();
            
            return;
        }
    }
    catch (ConcurrencyException) when (retries > 0)
    {
        // Retry with backoff
        Thread.Sleep(100);
    }
    }
    
    throw new Exception("Failed to update product after retries");
}
```

### 5. Don't Mix Sessions

```csharp
// Avoid: Mixing entities from different sessions
using var session1 = store.OpenSession();
    var product = session1.Load<Product>("p1");
    
    using (var session2 = store.OpenSession())
    {
    // Don't store entity from session1 in session2
    session2.Store(product);  // Bad!

// Good: Use entities within their session
using var session = store.OpenSession();`n`n    var product = session.Load<Product>("p1");
    product.Price = 99.99m;
    session.Store(product);
    session.SaveChanges();

```

## Troubleshooting

### Document Not Found

```csharp
using var session = store.OpenSession();`n`n    var product = session.Load<Product>("non-existent-id");
    
    if (product == null)
    {
    // Handle missing document
    throw new NotFoundException("Product not found");
```

### Duplicate Key Errors

```csharp
try
{
    using (var session = store.OpenSession())
    {
    session.Store(new Product { Id = "p1", Name = "Widget" });
    session.SaveChanges();
catch (SqlException ex) when (ex.Number == 2627)
{
    // Primary key violation
    // Document with this ID already exists
}
```

### Session State Errors

```csharp
// Avoid: Reusing session after save
using var session = store.OpenSession();`n`n    session.Store(new Product { Id = "p1", Name = "Widget" });
    session.SaveChanges();
    
    // After SaveChanges, session can still be used
    session.Store(new Product { Id = "p2", Name = "Gadget" });
    session.SaveChanges();
}
```

### Type Mismatch

```csharp
using var session = store.OpenSession();`n`n    // Trying to load as wrong type throws exception
    try
    {
    var product = session.Load<Product>("order-123");  // Actually an Order
    }
    catch (InvalidOperationException ex)
    {
    // Document exists but is not assignable to Product
```
