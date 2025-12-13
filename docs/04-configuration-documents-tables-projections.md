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
- An `Etag` column (for optimistic concurrency)
- A `Discriminator` column (for polymorphism)
- A `CreatedAt` column (creation timestamp)
- A `ModifiedAt` column (last modification timestamp)
- An `AwaitsReprojection` column (for schema migrations)
- A `Version` column (for document migrations)
- A `Document` column (stores the JSON)
- A `Metadata` column (for metadata storage)

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

Projections extract properties from documents into database columns for efficient querying.

### Simple Projections

Project a property using its name:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Name)
        .With(x => x.Price)
        .With(x => x.CategoryId);
});
```

This creates columns named `Name`, `Price`, and `CategoryId` that can be queried efficiently.

### Custom Column Names

You can specify a different name for the column:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With("ProductName", x => x.Name)
        .With("ProductPrice", x => x.Price);
});
```

This creates columns `ProductName` and `ProductPrice` in the database.

### Nested Properties

Project properties from nested objects:

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

### Transforming Values

Transform or convert property values before storing them in columns:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Price, price => Math.Round(price, 2))
        .With(x => x.Name, name => name.ToUpperInvariant())
        .With("ShippingCity", x => x.ShippingAddress.City, city => city.ToLowerInvariant());
});
```

The second parameter is a converter function that transforms the value. This works with both default and custom column names.

### Calculated Projections

Project computed values derived from multiple properties:

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With("FullName", x => $"{x.Brand} {x.Name}")
        .With("IsExpensive", x => x.Price > 1000)
        .With("PriceWithTax", x => x.Price * 1.25m);
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

**Important considerations:**
- **Automatic table assignment**: HybridDb automatically detects inheritance relationships. When you configure a type, HybridDb checks if it's a subtype of any previously registered type. If it is, it uses the same table as the base type's hierarchy. You can override this by explicitly specifying a different table name for the derived type.
- **Configuration order matters**: You must configure the base type before derived types. HybridDb detects inheritance by checking if a newly configured type is a subtype of an already-registered type. If you configure a derived type first, HybridDb won't find the base type and will create a separate table for it.
- **Nullable columns**: Projected properties from derived types create nullable columns. For example, the `Breed` column will be null for Cat documents, and the `Lives` column will be null for Dog and base Animal documents
- **Discriminator values**: By default, HybridDb uses the type's short name as the discriminator (e.g., "Animal", "Dog", "Cat")

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

> **⚠️ Deprecation Notice**: This feature will be removed in the next release. Use direct projections with `.With()` instead.

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

> **Note on Column Creation**: You should almost never create columns manually using `new Column()` or `design.Table.Add()`. HybridDb automatically handles column creation, nullability, and types when you use projections with `.With()`. Manual column manipulation is only needed in very rare advanced scenarios involving custom migrations or non-standard table structures.