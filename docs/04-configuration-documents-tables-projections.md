# Configuration - Documents, Tables and Projections

## Document Configuration

Documents are the core entities stored in HybridDb. Each document type must be registered with the store to configure its table and projected properties.

> **Note on Initialization**: By default, `DocumentStore.Create()` and `DocumentStore.ForTesting()` automatically initialize the store (creating tables and running migrations). You only need to call `store.Initialize()` manually if you pass `initialize: false` to the factory method. This is useful when you need to configure the store from multiple places before initialization.

### Basic Document Registration

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>();
});
```

This creates a table named "Products" (pluralized by convention) with:
- An `Id` column (primary key)
- A `Discriminator` column (for polymorphism)
- An `AwaitsReprojection` column (for migrations)
- An `Version` column (for optimistic concurrency)
- A `Document` column (stores the JSON)
- An `Metadata` column (for metadata storage)

### Custom Table Names

Override the default table name:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>("MyProductTable");
});
```

### Custom Discriminators

Specify a custom discriminator for a document type:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>(discriminator: "Prod");
});
```

Discriminators are used for:
- Polymorphic queries
- Document type identification
- Type mapping during deserialization

## Projections (Projected Properties)

Projections extract properties from documents into indexed columns for efficient querying.

### Simple Projections

Project a property:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Name)
        .With(x => x.Price)
        .With(x => x.CategoryId);
});
```

This creates columns for `Name`, `Price`, and `CategoryId` that can be queried efficiently.

### Named Projections

Specify a custom column name:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With("ProductName", x => x.Name);
});
```

### Projections with Converters

Transform the value before storing:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Price, price => Math.Round(price, 2))
        .With(x => x.Name, name => name?.ToUpperInvariant());
});
```

### Complex Property Projections

Project nested properties:

```csharp
public class Order
{
    public string Id { get; set; }
    public Address ShippingAddress { get; set; }
    public decimal Total { get; set; }
}

public class Address
{
    public string City { get; set; }
    public string Country { get; set; }
}

var store = DocumentStore.Create(config =>
{
    config.Document<Order>()
        .With("ShippingCity", x => x.ShippingAddress.City)
        .With("ShippingCountry", x => x.ShippingAddress.Country)
        .With(x => x.Total);
});
```

**Important**: HybridDb automatically injects null checks, so `x.ShippingAddress.City` won't throw if `ShippingAddress` is null.

### Calculated Projections

Project computed values:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With("FullName", x => $"{x.Brand} {x.Name}")
        .With("IsExpensive", x => x.Price > 1000);
});
```

### JSON Projections

Store complex objects as JSON in a column:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Tags, x => x, new AsJson());

    // Or with custom column name
    config.Document<Product>()
        .With("ProductTags", x => x.Tags, x => x, new AsJson());
});
```

This is useful for complex nested objects that you want to index but don't need to query on individual properties.

### Projection Options

#### MaxLength

Set maximum length for string columns:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Description, new MaxLength(1000));
});

// Default for strings is 850 if not specified
```

#### AsJson

Store value as JSON:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Specifications, new AsJson());
});
```

#### DisableNullCheckInjection

Disable automatic null checking:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Name, new DisableNullCheckInjection());
});
```

Use this if you're sure the property path won't be null, or if you want to handle nulls explicitly.

## Custom Keys

By default, HybridDb looks for an `Id` property. You can customize this:

### Using Key Method

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .Key(x => x.ProductCode);
});
```

### Using Global Key Resolver

```csharp
var store = DocumentStore.Create(config =>
{
    config.UseKeyResolver(entity =>
    {
        return entity switch
        {
            Product p => p.ProductCode,
            Order o => o.OrderNumber,
            _ => entity.GetType().GetProperty("Id")?.GetValue(entity)?.ToString()
        };
    });
});
```

## Polymorphic Documents

HybridDb supports polymorphic document hierarchies stored in the same table.

### Basic Polymorphism

```csharp
public abstract class Animal
{
    public string Id { get; set; }
    public string Name { get; set; }
}

public class Dog : Animal
{
    public string Breed { get; set; }
}

public class Cat : Animal
{
    public int Lives { get; set; }
}

var store = DocumentStore.Create(config =>
{
    // Configure the base type
    config.Document<Animal>()
        .With(x => x.Name);

    // Configure derived types
    config.Document<Dog>()
        .With(x => x.Breed);

    config.Document<Cat>()
        .With(x => x.Lives);
});
```

All animals are stored in the "Animals" table with different discriminators.

### Querying Polymorphic Types

```csharp
// Query all animals
var allAnimals = session.Query<Animal>().ToList();

// Query only dogs
var dogs = session.Query<Dog>().ToList();

// Query with type filtering
var bigDogs = session.Query<Animal>()
    .OfType<Dog>()
    .Where(d => d.Breed == "Great Dane")
    .ToList();
```

### Separate Tables for Derived Types

Sometimes you want derived types in separate tables:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Animal>("Animals");
    config.Document<Dog>("Dogs");  // Separate table
    config.Document<Cat>("Cats");  // Separate table
});
```

## Table Design

### Document Table Structure

Every document table includes these standard columns:

| Column | Type | Purpose |
|--------|------|---------|
| Id | nvarchar(850) | Primary key |
| Discriminator | nvarchar(850) | Type discriminator |
| Etag | uniqueidentifier | Optimistic concurrency |
| CreatedAt | datetime2 | Creation timestamp |
| ModifiedAt | datetime2 | Last modification timestamp |
| AwaitsReprojection | bit | Migration flag |
| Version | int | Migration version |
| Document | nvarchar(max) | JSON document |
| Metadata | nvarchar(max) | Document metadata |

Plus any projected columns you've configured.

### Accessing Table Configuration

```csharp
var design = store.Configuration.GetDesignFor<Product>();
var table = design.Table;

Console.WriteLine($"Table: {table.Name}");
foreach (var column in table.Columns)
{
    Console.WriteLine($"  {column.Name}: {column.Type}");
}
```

## Index Design

### Projections and Indexes

Projections create columns, but you may want to add indexes for performance:

```csharp
public class MyMigration : Migration
{
    public MyMigration() : base(1) { }
    
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
    {
        yield return new RawSqlCommand(
            "CREATE INDEX IX_Products_CategoryId ON Products (CategoryId)");
        
        yield return new RawSqlCommand(
            "CREATE INDEX IX_Products_Name ON Products (Name)");
    }
}

var store = DocumentStore.Create(config =>
{
    config.UseMigrations(new MyMigration());
});
```

### Composite Indexes

For queries on multiple columns:

```csharp
public class MyMigration : Migration
{
    public MyMigration() : base(1) { }
    
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
    {
        yield return new RawSqlCommand(@"
            CREATE INDEX IX_Products_Category_Price 
            ON Products (CategoryId, Price)");
```

## Extended Projections

Use the `Extend` method to add projections from related types:

```csharp
public class ProductIndex
{
    public string Category { get; set; }
    public string Supplier { get; set; }
}

var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .Extend<ProductIndex>(index =>
        {
            index.With(x => x.Category)
                 .With(x => x.Supplier);
        });
});
```

This is useful for organizing projections when you have many projected properties.

## Advanced Scenarios

### Multi-tenant Tables

Use table prefixes for multi-tenancy:

```csharp
public class TenantStore
{
    public static DocumentStore CreateForTenant(string tenantId)
    {
        return DocumentStore.Create(config =>
        {
            config.UseConnectionString(connectionString);
            config.UseTableNamePrefix($"{tenantId}_");
        });

// Each tenant gets isolated tables:
// Tenant1_Products, Tenant1_Orders
// Tenant2_Products, Tenant2_Orders
```

### Projection with Metadata

Access document metadata in projections:

```csharp
var projection = Projection.From<string>((document, metadata) =>
{
    var product = document as Product;
    var createdBy = metadata.TryGetValue("CreatedBy", out var value) 
        ? value.FirstOrDefault() 
        : "Unknown";
    
    return $"{product?.Name} (by {createdBy})";
});
```

### Nullable Columns

Control nullability explicitly:

```csharp
// Create a nullable column for a value type
var column = new Column("Score", typeof(int?));

// Or configure in projection
design.Table.Add(new Column<int?>("Score", nullable: true));
```

### Default Values

Set default values for columns:

```csharp
var column = new Column(
    name: "Status", 
    type: typeof(string),
    defaultValue: "Pending",
    nullable: false
);

design.Table.Add(column);
```

## Best Practices

### 1. Index Selectively

Only create projections for properties you query on:

```csharp
var store = DocumentStore.Create(config =>
{
    // Good: Only project what you query
    config.Document<Product>()
        .With(x => x.CategoryId)  // Frequently queried
        .With(x => x.Price);       // Frequently queried

    // Avoid: Projecting everything
    config.Document<Product>()
        .With(x => x.CategoryId)
        .With(x => x.Price)
        .With(x => x.Description)  // Rarely queried, waste of space
        .With(x => x.Notes);       // Rarely queried
});
```

### 2. Use Appropriate String Lengths

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Name, new MaxLength(200))      // Short names
        .With(x => x.Description, new MaxLength(4000)); // Longer descriptions
});
```

### 3. Consider Column Types

HybridDb automatically maps types, but be aware:

```csharp
// These are efficiently stored
.With(x => x.Price)        // decimal -> decimal
.With(x => x.IsActive)     // bool -> bit
.With(x => x.CreatedAt)    // DateTime -> datetime2
.With(x => x.Count)        // int -> int
.With(x => x.Id)           // Guid -> uniqueidentifier

// These require JSON serialization
.With(x => x.Tags, new AsJson())         // List<string>
.With(x => x.Settings, new AsJson())     // Complex object
```

### 4. Name Columns Clearly

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Order>()
        .With("CustomerId", x => x.Customer.Id)
        .With("CustomerName", x => x.Customer.Name)
        .With("ShippingCity", x => x.ShippingAddress.City);
});
```

### 5. Design for Queries

Think about your query patterns when designing projections:

```csharp
var store = DocumentStore.Create(config =>
{
    // If you query by date range and status
    config.Document<Order>()
        .With(x => x.OrderDate)
        .With(x => x.Status);

    // Consider adding a composite index in a migration
    // CREATE INDEX IX_Orders_Date_Status ON Orders (OrderDate, Status)
});
```

### 6. Use Discriminators Wisely

For polymorphic hierarchies, use clear discriminators:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<PaymentMethod>(discriminator: "Payment");
    config.Document<CreditCard>(discriminator: "Payment.CreditCard");
    config.Document<BankTransfer>(discriminator: "Payment.BankTransfer");
});
```

## Troubleshooting

### Projection Not Working

If a projection doesn't create a column:
1. Ensure the store is initialized after configuration
2. Check that the projection path is valid
3. Verify the return type is supported

### Null Reference in Projection

If you get null reference errors:
- HybridDb should auto-inject null checks
- If not, ensure you're not using `DisableNullCheckInjection`
- Check that the projection expression is a simple property path

### Type Mismatch Errors

If you get type mismatch errors:
- Ensure converter return type matches column type
- Check that nullable types are handled correctly
- Verify enum types are projected correctly

### Performance Issues

If projections are slow:
- Add database indexes on frequently queried columns
- Reduce the number of projections
- Use simpler projection expressions
- Consider batching document migrations
