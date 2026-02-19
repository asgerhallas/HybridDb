# Getting Started

## Introduction

HybridDb is a lightweight document database library built on top of SQL Server. It provides a simple, unit-of-work style API similar to NHibernate or RavenDB, while leveraging the reliability and transactional capabilities of SQL Server.

### Key Features

- **Simple API**: Store and query semi-structured data with minimal configuration
- **Schema-less Storage**: Persist .NET objects as JSON without complex mappings
- **Projected Columns**: Project document properties into columns for efficient querying
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

HybridDb includes Newtonsoft.Json for JSON serialization by default.

## Quick Start

### Basic Example

Here's a simple example to get started with HybridDb:

<!-- snippet: QuickStart_BasicExample -->
<a id='snippet-QuickStart_BasicExample'></a>

```cs
// Create a document store for testing (uses temp tables)
var store = DocumentStore.ForTesting(TableMode.GlobalTempTables, configuration => 
{
    configuration.Document<Entity>().With(x => x.Property);
});

// Open a session
using var session = store.OpenSession();

// Store a document
session.Store(new Entity 
{ 
    Id = Guid.NewGuid().ToString(), 
    Property = "Hello", 
    Number = 2001 
});

// Save changes to the database
session.SaveChanges();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc01_GettingStartedTests.cs#L22-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-QuickStart_BasicExample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Production Setup

For production use, create a store with real tables:

<!-- snippet: ProductionSetup -->
<a id='snippet-ProductionSetup'></a>

```cs
var store = DocumentStore.Create(configuration =>
{
    configuration.UseConnectionString(
        "Server=localhost;Database=MyApp;Integrated Security=True;Encrypt=False;");
    
    // Configure documents
    configuration.Document<Product>()
        .With(x => x.Name)
        .With(x => x.Price);
    
    configuration.Document<Order>()
        .With(x => x.CustomerId)
        .With(x => x.OrderDate);
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc01_GettingStartedTests.cs#L61-L76' title='Snippet source file'>snippet source</a> | <a href='#snippet-ProductionSetup' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Core Concepts

### DocumentStore

The `DocumentStore` is the entry point for HybridDb. It:
- Manages the database connection and transactions
- Holds configuration for documents and tables
- Creates sessions for working with documents
- Manages schema and document migrations
- This should live for the lifetime of the application (e.g. registered as a singleton)

There are two factory methods:
- `DocumentStore.Create()`: Creates a store with real database tables (for production)
- `DocumentStore.ForTesting()`: Creates a store with global temp tables or real tables (for testing)

### DocumentSession

The `DocumentSession` represents a unit of work and acts as a first-level cache. It:
- Tracks loaded and stored documents
- Manages entity changes
- Provides LINQ query capabilities
- Batches all changes until `SaveChanges()` is called
- This should live for the lifetime of the operation (e.g. registered per request or command execution)

### Document Configuration

Documents must be registered with the store and can have projected properties:

<!-- snippet: DocumentConfiguration -->
<a id='snippet-DocumentConfiguration'></a>

```cs
// Create a table named Products and add a database column for the Name, Price and CategoryId properties and keep the values up-to-date on each call to Session.SaveChanges()
configuration.Document<Product>()
    .With(x => x.Name)
    .With(x => x.Price)
    .With(x => x.CategoryId);
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc01_GettingStartedTests.cs#L84-L90' title='Snippet source file'>snippet source</a> | <a href='#snippet-DocumentConfiguration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Table Modes

HybridDb supports different table modes for different scenarios:

- **TableMode.RealTables**: Creates real database tables (production use and advanced testing scenarios)
- **TableMode.GlobalTempTables**: Uses global temp tables in TempDb (for testing only)

## Next Steps

Now that you understand the basics, explore these topics:

1. **[Configuration](Configuration-Connections-and-Testing.md)**: Learn about connection strings, testing setup, and advanced configuration
2. **[Documents and Projections](Configuration-Documents-Tables-and-Projections.md)**: Deep dive into document configuration and indexing
3. **[Migrations](Migrations.md)**: Understand how to handle schema and data changes
4. **[DocumentSession](DocumentSession-Store-and-Load.md)**: Master storing, loading, and querying documents
5. **[Advanced Scenarios](10-documentsession-advanced.md)**: Explore advanced features like transactions, eviction, and metadata

## Common Patterns

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

