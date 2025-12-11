# Getting Started

## Introduction

HybridDb is a lightweight document database library built on top of SQL Server. It provides a simple, unit-of-work style API similar to NHibernate or RavenDB, while leveraging the reliability and transactional capabilities of SQL Server.

### Key Features

- **Simple API**: Store and query semi-structured data with minimal configuration
- **Schema-less Storage**: Persist .NET objects without complex mappings
- **Queryable Indexes**: Index document properties for efficient querying
- **LINQ Support**: Query documents using familiar LINQ syntax
- **ACID Transactions**: Full consistency and transactionality of SQL Server
- **Schema Migrations**: Built-in tools for handling document and schema changes
- **Event Store**: Optional event sourcing capabilities
- **Message Queue**: Built-in message queue functionality

## Installation

Install HybridDb via NuGet:

```bash
dotnet add package HybridDb
```

For JSON serialization support using Newtonsoft.Json:

```bash
dotnet add package HybridDb.NewtonsoftJson
```

## Quick Start

### Basic Example

Here's a simple example to get started with HybridDb:

```csharp
using HybridDb;

// Define your entity
public class Entity
{
    public Guid Id { get; set; }
    public string Property { get; set; }
    public int Field { get; set; }
}

// Create a document store for testing (uses temp tables)
var store = DocumentStore.ForTesting(TableMode.TempTables);

// Configure the document and its indexed properties
store.Document<Entity>().With(x => x.Property);

// Use the store
using var session = store.OpenSession();`n`n    // Store a document
    session.Store(new Entity 
    { 
        Id = Guid.NewGuid(), 
        Property = "Hello", 
        Field = 2001 
    });
    
    session.SaveChanges();
}

using var session = store.OpenSession();`n`n    // Query documents using LINQ
    var entity = session.Query<Entity>()
        .Single(x => x.Property == "Hello");
    
    // Update the entity
    entity.Field++;
    
    session.SaveChanges();
}
```

### Production Setup

For production use, create a store with real tables:

```csharp
var store = DocumentStore.Create(configuration =>
{
    configuration.UseConnectionString(
        "Server=localhost;Database=MyApp;Integrated Security=True;");
    
    // Configure documents
    configuration.Document<Entity>()
        .With(x => x.Property)
        .With(x => x.Field);
});
```

## Core Concepts

### DocumentStore

The `DocumentStore` is the entry point for HybridDb. It:
- Manages the database connection
- Holds configuration for documents and tables
- Creates sessions for working with documents
- Manages schema and document migrations

There are two factory methods:
- `DocumentStore.Create()`: Creates a store with real database tables (for production)
- `DocumentStore.ForTesting()`: Creates a store with temp tables or global temp tables (for testing)

### DocumentSession

The `DocumentSession` represents a unit of work and acts as a first-level cache. It:
- Tracks loaded and stored documents
- Manages entity changes
- Provides LINQ query capabilities
- Batches all changes until `SaveChanges()` is called

### Document Configuration

Documents must be registered with the store and can have indexed properties:

```csharp
store.Document<Product>()
    .With(x => x.Name)           // Index the Name property
    .With(x => x.Price)          // Index the Price property
    .With(x => x.Category)       // Index the Category property
    .Key(x => x.ProductCode);    // Use custom key instead of Id property
```

### Table Modes

HybridDb supports different table modes for different scenarios:

- **TableMode.RealTables**: Creates real database tables (production use)
- **TableMode.GlobalTempTables**: Uses global temp tables in TempDb (testing/isolation)

## Next Steps

Now that you understand the basics, explore these topics:

1. **[Configuration](03-configuration-connections-testing.md)**: Learn about connection strings, testing setup, and advanced configuration
2. **[Documents and Projections](04-configuration-documents-tables-projections.md)**: Deep dive into document configuration and indexing
3. **[Migrations](06-migrations.md)**: Understand how to handle schema and data changes
4. **[DocumentSession](08-documentsession-store-load.md)**: Master storing, loading, and querying documents
5. **[Advanced Scenarios](10-documentsession-advanced.md)**: Explore advanced features like transactions, eviction, and metadata

## Common Patterns

### Repository Pattern

```csharp
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
```

### Using Transactions

```csharp
// Begin a transaction
using var tx = store.BeginTransaction();

try
{
    using var session1 = store.OpenSession(tx);
    var entity = session1.Load<Entity>("some-id");
    entity.Property = "Updated";
    session1.SaveChanges();
    
    using var session2 = store.OpenSession(tx);
    session2.Store(new AnotherEntity { Id = "new-id" });
    session2.SaveChanges();
    
    // Commit the transaction
    tx.Commit();
}
catch
{
    tx.Rollback();
    throw;
}
```

## Troubleshooting

### Store Not Initialized

The store is automatically initialized when created. If you explicitly disabled initialization:

```csharp
var store = DocumentStore.Create(config => { /* ... */ }, initialize: false);
```

You must manually initialize it before use:

```csharp
store.Initialize();
```

### Connection String Issues

Ensure your connection string is valid and includes `Encrypt=False` for local SQL Server:

```csharp
configuration.UseConnectionString(
    "Server=localhost;Database=MyDb;Integrated Security=True;Encrypt=False;");
```

### Missing Projections

If queries on properties don't work, ensure you've configured them:

```csharp
store.Document<Entity>()
    .With(x => x.PropertyToQuery);  // Required for querying
```
