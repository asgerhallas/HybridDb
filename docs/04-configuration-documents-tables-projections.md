# Configuration - Documents, Tables and Projections

## Document Configuration

Documents are the core entities stored in HybridDb. Each document type must be registered with the store to configure its table and projected properties.

> **Note on Initialization**: By default, `DocumentStore.Create()` and `DocumentStore.ForTesting()` automatically initialize the store (creating tables and running migrations). You only need to call `store.Initialize()` manually if you pass `initialize: false` to the factory method. This is useful when you need to configure the store from multiple places before initialization.

### Basic Document Registration

<!-- snippet: BasicDocumentRegistration -->
<a id='snippet-BasicDocumentRegistration'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<Product>();
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L19-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-BasicDocumentRegistration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

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

<!-- snippet: CustomTableNames -->
<a id='snippet-CustomTableNames'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<Product>("MyProductTable");
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L32-L37' title='Snippet source file'>snippet source</a> | <a href='#snippet-CustomTableNames' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Custom Discriminators

Specify a custom discriminator for a document type:

<!-- snippet: CustomDiscriminators -->
<a id='snippet-CustomDiscriminators'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<Product>(discriminator: "Prod");
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L45-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-CustomDiscriminators' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Discriminators are used for:
- Polymorphic queries
- Document type identification
- Type mapping during deserialization

## Projections (Projected Properties)

Projections extract properties from documents into database columns for efficient querying.

### Simple Projections

Project a property using its name:

<!-- snippet: SimpleProjections -->
<a id='snippet-SimpleProjections'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With(x => x.Name)
        .With(x => x.Price)
        .With(x => x.CategoryId);
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L58-L66' title='Snippet source file'>snippet source</a> | <a href='#snippet-SimpleProjections' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This creates columns named `Name`, `Price`, and `CategoryId` that can be queried efficiently.

### Custom Column Names

You can specify a different name for the column:

<!-- snippet: CustomColumnNames -->
<a id='snippet-CustomColumnNames'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .With("ProductName", x => x.Name)
        .With("ProductPrice", x => x.Price);
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L74-L81' title='Snippet source file'>snippet source</a> | <a href='#snippet-CustomColumnNames' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This creates columns `ProductName` and `ProductPrice` in the database.

### Nested Properties

Project properties from nested objects:

<!-- snippet: NestedProperties -->
<a id='snippet-NestedProperties'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<Order>()
        .With("ShippingCity", x => x.ShippingAddress.City)
        .With("ShippingCountry", x => x.ShippingAddress.Country)
        .With(x => x.Total);
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L89-L97' title='Snippet source file'>snippet source</a> | <a href='#snippet-NestedProperties' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Important**: HybridDb automatically injects null checks, so `x.ShippingAddress.City` won't throw if `ShippingAddress` is null.

### Transforming Values

Transform or convert property values before storing them in columns:

<!-- snippet: TransformingValues -->
<a id='snippet-TransformingValues'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<ExtendedProduct>()
        .With(x => x.Price, price => Math.Round(price, 2))
        .With(x => x.Name, name => name.ToUpperInvariant())
        .With("ShippingCity", x => x.ShippingAddress.City, city => city.ToLowerInvariant());
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L105-L113' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransformingValues' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The second parameter is a converter function that transforms the value. This works with both default and custom column names.

### Calculated Projections

Project computed values derived from multiple properties:

<!-- snippet: CalculatedProjections -->
<a id='snippet-CalculatedProjections'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<ExtendedProduct>()
        .With("FullName", x => $"{x.Brand} {x.Name}")
        .With("IsExpensive", x => x.Price > 1000)
        .With("PriceWithTax", x => x.Price * 1.25m);
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L121-L129' title='Snippet source file'>snippet source</a> | <a href='#snippet-CalculatedProjections' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### JSON Projections

Store complex objects as JSON in a column:

<!-- snippet: JsonProjections -->
<a id='snippet-JsonProjections'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<ExtendedProduct>()
        .With(x => x.Tags, x => x, new AsJson());

    // Or with custom column name
    config.Document<ExtendedProduct>()
        .With("ProductTags", x => x.Tags, x => x, new AsJson());
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L137-L147' title='Snippet source file'>snippet source</a> | <a href='#snippet-JsonProjections' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This is useful for complex nested objects that you want to index but don't need to query on individual properties.

### Projection Options

#### MaxLength

Set maximum length for string columns:

<!-- snippet: MaxLengthOption -->
<a id='snippet-MaxLengthOption'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<ExtendedProduct>()
        .With(x => x.Description, new MaxLength(1000));
});

// Default for strings is 850 if not specified
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L155-L163' title='Snippet source file'>snippet source</a> | <a href='#snippet-MaxLengthOption' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### AsJson

Store value as JSON:

<!-- snippet: AsJsonOption -->
<a id='snippet-AsJsonOption'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<ExtendedProduct>()
        .With(x => x.Specifications, new AsJson());
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L171-L177' title='Snippet source file'>snippet source</a> | <a href='#snippet-AsJsonOption' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### DisableNullCheckInjection

Disable automatic null checking:

<!-- snippet: DisableNullCheckInjectionOption -->
<a id='snippet-DisableNullCheckInjectionOption'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<ExtendedProduct>()
        .With(x => x.Name, new DisableNullCheckInjection());
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L185-L191' title='Snippet source file'>snippet source</a> | <a href='#snippet-DisableNullCheckInjectionOption' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Use this if you're sure the property path won't be null, or if you want to handle nulls explicitly.

## Custom Keys

By default, HybridDb looks for an `Id` property. You can customize this:

### Using Key Method

```csharp
var store = DocumentStore.Create(config =>
{
    config.Document<ExtendedProduct>()
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
            ExtendedProduct p => p.ProductCode,
            Order o => o.OrderNumber,
            _ => entity.GetType().GetProperty("Id")?.GetValue(entity)?.ToString()
        };
    });
});
```

## Polymorphic Documents

HybridDb supports polymorphic document hierarchies stored in the same table.

### Basic Polymorphism

<!-- snippet: AnimalHierarchy -->
<a id='snippet-AnimalHierarchy'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L440-L456' title='Snippet source file'>snippet source</a> | <a href='#snippet-AnimalHierarchy' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: BasicPolymorphism -->
<a id='snippet-BasicPolymorphism'></a>

```cs
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
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L234-L248' title='Snippet source file'>snippet source</a> | <a href='#snippet-BasicPolymorphism' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

All animals are stored in the "Animals" table with different discriminators.

**Important considerations:**
- **Automatic table assignment**: HybridDb automatically detects inheritance relationships. When you configure a type, HybridDb checks if it's a subtype of any previously registered type. If it is, it uses the same table as the base type's hierarchy. You can override this by explicitly specifying a different table name for the derived type.
- **Configuration order matters**: You must configure the base type before derived types. HybridDb detects inheritance by checking if a newly configured type is a subtype of an already-registered type. If you configure a derived type first, HybridDb won't find the base type and will create a separate table for it.
- **Nullable columns**: Projected properties from derived types create nullable columns. For example, the `Breed` column will be null for Cat documents, and the `Lives` column will be null for Dog and base Animal documents
- **Discriminator values**: By default, HybridDb uses the type's short name as the discriminator (e.g., "Animal", "Dog", "Cat")

### Querying Polymorphic Types

<!-- snippet: QueryingPolymorphicTypes -->
<a id='snippet-QueryingPolymorphicTypes'></a>

```cs
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
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L262-L274' title='Snippet source file'>snippet source</a> | <a href='#snippet-QueryingPolymorphicTypes' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Separate Tables for Derived Types

Sometimes you want derived types in separate tables:

<!-- snippet: SeparateTablesForDerivedTypes -->
<a id='snippet-SeparateTablesForDerivedTypes'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<Animal>("Animals");
    config.Document<Dog>("Dogs");  // Separate table
    config.Document<Cat>("Cats");  // Separate table
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L284-L291' title='Snippet source file'>snippet source</a> | <a href='#snippet-SeparateTablesForDerivedTypes' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

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

<!-- snippet: AccessingTableConfiguration -->
<a id='snippet-AccessingTableConfiguration'></a>

```cs
var design = store.Configuration.GetDesignFor<Product>();
var table = design.Table;

Console.WriteLine($"Table: {table.Name}");
foreach (var column in table.Columns)
{
    Console.WriteLine($"  {column.Name}: {column.Type}");
}
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L299-L308' title='Snippet source file'>snippet source</a> | <a href='#snippet-AccessingTableConfiguration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Index Design

### Projections and Indexes

Projections create columns, but you may want to add indexes for performance:

<!-- snippet: MyMigration -->
<a id='snippet-MyMigration'></a>

```cs
public class MyMigration : Migration
{
    public MyMigration() : base(1) { }
    
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
    {
        yield return new SqlCommand(
            "CREATE INDEX IX_Products_CategoryId ON Products (CategoryId)",
            (sql, db) => sql.Append("CREATE INDEX IX_Products_CategoryId ON Products (CategoryId)"));
        
        yield return new SqlCommand(
            "CREATE INDEX IX_Products_Name ON Products (Name)",
            (sql, db) => sql.Append("CREATE INDEX IX_Products_Name ON Products (Name)"));
    }
}
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L381-L397' title='Snippet source file'>snippet source</a> | <a href='#snippet-MyMigration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: ProjectionsAndIndexes -->
<a id='snippet-ProjectionsAndIndexes'></a>

```cs
var migration = new MyMigration();

var store = DocumentStore.Create(config =>
{
    config.Document<Product>();
    config.UseMigrations(new List<Migration> { migration });
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L316-L324' title='Snippet source file'>snippet source</a> | <a href='#snippet-ProjectionsAndIndexes' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Composite Indexes

For queries on multiple columns:

<!-- snippet: CompositeIndexMigration -->
<a id='snippet-CompositeIndexMigration'></a>

```cs
public class CompositeIndexMigration : Migration
{
    public CompositeIndexMigration() : base(1) { }
    
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
    {
        yield return new SqlCommand(
            "CREATE INDEX IX_Products_Category_Price ON Products (CategoryId, Price)",
            (sql, db) => sql.Append(@"
                CREATE INDEX IX_Products_Category_Price 
                ON Products (CategoryId, Price)"));
    }
}
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L399-L413' title='Snippet source file'>snippet source</a> | <a href='#snippet-CompositeIndexMigration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Extended Projections

> **⚠️ Deprecation Notice**: This feature will be removed in the next release. Use direct projections with `.With()` instead.

Use the `Extend` method to add projections from related types:

<!-- snippet: ExtendedProjections -->
<a id='snippet-ExtendedProjections'></a>

```cs
var store = DocumentStore.Create(config =>
{
    config.Document<Product>()
        .Extend<ProductIndex>(index =>
        {
            index.With(x => x.Category, p => p.Category);
            index.With(x => x.Supplier, p => p.Category);
        });
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L348-L358' title='Snippet source file'>snippet source</a> | <a href='#snippet-ExtendedProjections' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This is useful for organizing projections when you have many projected properties.

## Advanced Scenarios

### Projection with Metadata

Access document metadata in projections:

<!-- snippet: ProjectionWithMetadata -->
<a id='snippet-ProjectionWithMetadata'></a>

```cs
var projection = Projection.From<string>((document, metadata) =>
{
    var product = document as Product;
    var createdBy = metadata.TryGetValue("CreatedBy", out var value) 
        ? value.FirstOrDefault() 
        : "Unknown";
    
    return $"{product?.Name} (by {createdBy})";
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc04_DocumentsTablesProjectionsTests.cs#L366-L376' title='Snippet source file'>snippet source</a> | <a href='#snippet-ProjectionWithMetadata' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

> **Note on Column Creation**: You should almost never create columns manually using `new Column()` or `design.Table.Add()`. HybridDb automatically handles column creation, nullability, and types when you use projections with `.With()`. Manual column manipulation is only needed in very rare advanced scenarios involving custom migrations or non-standard table structures.
