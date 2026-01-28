# HybridDb Documentation

Welcome to the HybridDb documentation! This documentation provides comprehensive guidance for using HybridDb, a document database library built on SQL Server.

## Table of Contents

### Getting Started
1. **[Getting Started](Getting-Started.md)** - Introduction, installation, quick start, and core concepts
2. **[How to Build and Contribute](How-to-Build-and-Contribute.md)** - Development setup, building, testing, and contribution guidelines

### Configuration
3. **[Connections and Testing](Configuration-Connections-and-Testing.md)** - Connection strings, testing strategies, and environment-specific configuration
4. **[Documents, Tables and Projections](Configuration-Documents-Tables-and-Projections.md)** - Document configuration, table design, and property projections
5. **[Events](Configuration-Events.md)** - Internal event handlers

### Store and Transactions
7. **[DocumentStore and DocumentTransaction](DocumentStore-and-Transaction.md)** - Store lifecycle, transaction management, and isolation levels

### Working with Documents
8. **[DocumentSession - Store and Load](DocumentSession-Store-and-Load.md)** - Basic CRUD operations, entity states, and session lifecycle
9. **[DocumentSession - Query](DocumentSession-Query.md)** - LINQ queries, raw SQL queries, and query optimization
10. **[DocumentSession - Concurrency Model](DocumentSession-Concurrency-Model.md)** - Optimistic concurrency, ETags, and conflict detection
11. **[DocumentSession - Advanced Scenarios](DocumentSession-Advanced.md)** - Metadata, ETags, deferred commands, and advanced patterns

### Migrations
6. **[Migrations](Migrations.md)** - Schema migrations, document migrations, and migration patterns

## Quick Links

### Common Tasks

**Installation**
```bash
dotnet add package HybridDb
```

**Basic Usage**
```csharp
var store = DocumentStore.ForTesting(TableMode.TempTables);
store.Document<Product>().With(x => x.Name);

using var session = store.OpenSession();

session.Store(new Product { Id = "p1", Name = "Widget" });
session.SaveChanges();
```

**Querying**
```csharp
using var session = store.OpenSession();

var products = session.Query<Product>()
    .Where(x => x.Price > 100)
    .OrderBy(x => x.Name)
    .ToList();
```

### Key Features

- **Simple API**: Store and retrieve .NET objects with minimal configuration
- **LINQ Queries**: Query documents using familiar LINQ syntax
- **Schema Evolution**: Built-in migration support for schema and data changes
- **Transactions**: Full ACID transaction support via SQL Server
- **Event Store**: Optional event sourcing capabilities
- **Optimistic Concurrency**: ETags for conflict detection
- **Polymorphism**: Store and query document hierarchies
- **Projections**: Index document properties for efficient querying

## Learning Path

### Beginners
1. Start with [Getting Started](Getting-Started.md)
2. Learn about [Connections and Testing](Configuration-Connections-and-Testing.md)
3. Understand [Documents and Projections](Configuration-Documents-Tables-and-Projections.md)
4. Practice [Store and Load](DocumentSession-Store-and-Load.md) operations

### Intermediate
1. Master [Query](DocumentSession-Query.md) capabilities
2. Learn [Migrations](Migrations.md) for schema evolution
3. Understand [Transactions](DocumentStore-and-Transaction.md)
4. Explore [Advanced Scenarios](10-documentsession-advanced.md)

### Advanced
1. Implement [Event Store](Configuration-Events.md) patterns
2. Build complex migration strategies
3. Optimize query performance
4. Understand [Concurrency Model](DocumentSession-Concurrency-Model.md) for production systems
5. Contribute to the project with [How to Build and Contribute](How-to-Build-and-Contribute.md)

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    Your Application                      │
└─────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│                   DocumentSession                        │
│  (Unit of Work / First-level Cache)                     │
└─────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│                   DocumentStore                          │
│  (Configuration / Schema / Migrations)                  │
└─────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│                    SQL Server                            │
│  (Storage / Transactions / Indexes)                     │
└─────────────────────────────────────────────────────────┘
```

## Support and Resources

- **GitHub Repository**: [https://github.com/asgerhallas/HybridDb](https://github.com/asgerhallas/HybridDb)
- **Issues**: Report bugs and request features on GitHub Issues
- **NuGet Package**: [HybridDb on NuGet](https://www.nuget.org/packages/HybridDb/)
- **License**: MIT License

## What's Next?

- Read [Getting Started](Getting-Started.md) to begin using HybridDb
- Explore [Configuration](Configuration-Connections-and-Testing.md) options for your environment
- Learn about [Migrations](Migrations.md) for evolving your schema
- Dive into [Advanced Scenarios](10-documentsession-advanced.md) for complex use cases

---

*Documentation generated for HybridDb - A document database on SQL Server*
