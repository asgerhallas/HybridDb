# Migrations

## Overview

HybridDb provides a comprehensive migration system to handle changes to both the database schema and document data. Migrations ensure your database stays in sync with your code as it evolves.

There are two types of migrations:

1. **Schema Migrations**: Changes to database tables and columns
2. **Document Migrations**: Changes to document data and structure using JSON manipulation

## ⚠️ Critical: JSON-Based Migrations

**ALWAYS migrate documents by manipulating JSON directly, NOT by deserializing to C# objects.**

### Why This Matters

When you migrate documents, you might be tempted to deserialize them to your current C# model classes, modify the objects, and serialize them back. **This is dangerous and often fails.**

**Problems with Object-Based Migrations:**

Your current C# model may not match the old serialized JSON:
- Added/removed/renamed properties won't deserialize
- Changed property types cause runtime errors
- Model changes break previously-working migrations
- Missing assemblies or renamed types cause failures
- You'll waste time fixing compilation errors in old migrations whenever models change

**The Multi-Developer Team Problem:**

In a single-developer environment, it may be okay to use model-based migrations for convenience and delete them after they have run. This works since you control all the code and all the databases.

**However, in a multi-developer team this approach breaks down:**

1. **Databases are in different states**: Developer A might be on migration version 5, while Developer B is on version 3, and the CI/CD pipeline might be on version 7
2. **Temporal coupling**: When you commit a model-based migration that references `Product.NewField`, it works for you because you just added that field. But Developer B pulls your code while their database is still on an older migration state - the model has evolved further and the migration no longer compiles, or worse, it compiles but fails at runtime when their old data doesn't match the new model expectations
3. **You can't safely delete**: Even if you plan to delete the migration after running it locally, other developers haven't run it yet. Deleting it breaks their ability to migrate their databases forward
4. **Continuous breakage**: Every time the model evolves, old model-based migrations break. The team constantly fixes compilation errors in code that should be "write once, never touch"

**The real problem isn't just compilation errors** - it's that model-based migrations create an implicit dependency between your migration code and the current state of your models, which varies across team members and environments. JSON-based migrations eliminate this coupling entirely.

**Solution: JSON String Manipulation**

Work with the JSON as a string using `JObject` (or similar):

```csharp
// ✅ CORRECT - Manipulate JSON directly
new ChangeDocument<Product>((serializer, json) =>
{
    var doc = JObject.Parse(json);
    
    // Safe transformations on JSON structure
    doc["NewField"] = doc["OldField"];
    doc.Remove("OldField");
    
    return doc.ToString();
});

// ❌ WRONG - Don't deserialize to model classes
new InlineDocumentMigration<Product>((session, product) =>
{
    // This will fail if Product model changed incompatibly!
    product.NewField = product.OldField;
});
```

This approach is:
- **Version-agnostic**: Works regardless of current model definition
- **Type-safe**: No deserialization failures
- **Future-proof**: Won't break when models evolve

## Migration Basics

Migrations are defined by implementing the `Migration` class:

```csharp
public class MyMigration(int version) : Migration(version)
{
    public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
    {
        // Commands run BEFORE automatic schema migrations
        // Runs synchronously during store.Initialize()
        yield break;
    }
    
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
    {
        // Commands run AFTER automatic schema migrations
        // Runs synchronously during store.Initialize()
        yield break;
    }
    
    public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
    {
        // Commands to migrate document data
        // Runs ASYNCHRONOUSLY after store.Initialize() completes
        // Also runs on-demand when documents are loaded
        // You CANNOT know when these complete!
        yield break;
```

**Critical Migration Timing:**
- `BeforeAutoMigrations` → Auto Migrations → `AfterAutoMigrations` all run synchronously during `store.Initialize()`
- `Background` migrations start AFTER initialization and run asynchronously
- Background migrations may take hours or days to complete on large datasets
- You cannot rely on background migrations being complete at any specific time
- Never write code in `AfterAutoMigrations` or `BeforeAutoMigrations` that depends on `Background` migrations being complete

### Registering Migrations

Register migrations in your configuration:

```csharp
var store = DocumentStore.Create(config =>
{
    config.UseConnectionString(connectionString);
    
    config.UseMigrations(
        new AddIndexes(1),
        new UpdateProductSchema(2),
        new RenameField(3)
    );
});

store.Initialize();
```

### Migration Versioning

Migrations are versioned and run in order. Use descriptive class names and pass the version when instantiating:

```csharp
public class AddIndexes(int version) : Migration(version)
{
    // ...
}

public class UpdateProductSchema(int version) : Migration(version)
{
    // ...
}

// Usage:
config.UseMigrations(
    new AddIndexes(1),
    new UpdateProductSchema(2)
);
```

HybridDb tracks which migrations have been run in the metadata table and only runs new ones.

**Team Tip**: When multiple developers create migrations simultaneously, merge conflicts are easier to resolve if you only need to adjust the version number when instantiating (`new AddIndexes(5)`) rather than renaming the entire class.

## Schema Migrations

Schema migrations modify the database structure.

### Automatic Schema Migrations

HybridDb automatically creates and updates tables based on your document configuration:

```csharp
// Before: Only Name column exists
// First version
var store = DocumentStore.Create(config =>
{
    config.UseConnectionString(connectionString);
    config.Document<Product>()
        .With(x => x.Name);
});

// Later: Add Price column - HybridDb will automatically add it when initialized.
var store = DocumentStore.Create(config =>
{
    config.UseConnectionString(connectionString);
    config.Document<Product>()
        .With(x => x.Name)
        .With(x => x.Price);  // New column
});


```

HybridDb detects the difference and creates the `Price` column automatically.

### Before Auto Migrations

Run commands before automatic schema migrations:

```csharp
public class AddDefaultPriceColumn(int version) : Migration(version)
{
    public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
    {
        // Add column with default value before auto-migration
        yield return new AddColumn(
            "Products", 
            new Column("Price", typeof(decimal), defaultValue: 0m)
        );
```

**Use case**: Add columns with default values to avoid triggering re-projection.

### After Auto Migrations

Run commands after automatic schema migrations:

```csharp
public class AddIndexes(int version) : Migration(version)
{
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
    {
        // Add indexes after columns are created
        yield return new RawSqlCommand(@"
            CREATE INDEX IX_Products_CategoryId 
            ON Products (CategoryId)
        ");
        
        yield return new RawSqlCommand(@"
            CREATE INDEX IX_Products_Price 
            ON Products (Price) 
            WHERE Price > 0
        ");
```

**Use case**: Add indexes, constraints, or other schema enhancements after auto-migration.

### Schema DDL Commands

Available schema commands:

```csharp
// Create table
yield return new CreateTable(
    new Table("MyTable", 
        new Column("Id", typeof(string), isPrimaryKey: true),
        new Column("Data", typeof(string))
    )
);

// Add column
yield return new AddColumn("MyTable", 
    new Column("NewColumn", typeof(int), defaultValue: 0)
);

// Remove column
yield return new RemoveColumn("MyTable", "OldColumn");

// Rename column
yield return new RenameColumn("Products", "OldName", "NewName");

// Rename table
yield return new RenameTable("OldTableName", "NewTableName");

// Raw SQL
yield return new RawSqlCommand("ALTER TABLE Products ADD Status nvarchar(50)");
```

## Document Migrations

Document migrations transform document data as it's loaded or in the background.

### Background Migrations

Background migrations run asynchronously on all documents:

```csharp
public class UpdateProductSchema(int version) : Migration(version)
{
    public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
    {
        yield return new ChangeDocument<Product>((serializer, json) =>
        {
            // IMPORTANT: Migrate the JSON directly, not deserialized models
            // Models may have changed in incompatible ways or may change in the future
            var doc = JObject.Parse(json);
            
            // Add missing category
            if (doc["Category"] == null || string.IsNullOrEmpty(doc.Value<string>("Category")))
            {
                doc["Category"] = "Uncategorized";
            }
            
            // Fix negative prices
            if (doc["Price"] != null && doc.Value<decimal>("Price") < 0)
            {
                doc["Price"] = 0;
            }
            
            return doc.ToString();
        });
```

### ChangeDocument - JSON-Based Migrations

The `ChangeDocument<T>` class is the primary way to migrate documents. It works with JSON strings directly:

```csharp
// Product class is used here _only_ to fetch the correct documents from the correct tables. This is a convenience, but actually goes against the principles about not using models in migrations. It's rarely a problem in reality though - and if it is, implement and use RowMigrationCommand directly instead.
yield return new ChangeDocument<Product>((serializer, json) =>
{
    // Migrate JSON directly - models may have changed incompatibly
    var doc = JObject.Parse(json);
    
    // Transform the JSON structure
    doc["UpdatedField"] = doc.Value<string>("OldField")?.ToUpper();
    
    return doc.ToString();
});
```

**Advanced Usage with Session Access:**

```csharp
yield return new ChangeDocument<Product>((session, serializer, row, json) =>
{
    var doc = JObject.Parse(json);
    
    // Load related documents for cross-document operations
    var categoryId = doc.Value<string>("CategoryId");

    // This is also a violation of the principle not to use models directly in 
    // migrations, category might itself be migrated when loaded and that might in turn lead to very slow migrations, so use with care. 
    var category = session.Load<Category>(categoryId);
    
    // Use loaded data in migration
    if (category != null)
    {
        doc["CategoryName"] = category.Name;
    }
    
    return doc.ToString();
});
```

### Projection Updates

When you add or change projections, HybridDb automatically re-projects documents:

```csharp
// Version 1: No CategoryId projection
store.Configuration.Document<Product>()
    .With(x => x.Name);

// Version 2: Add CategoryId projection
store.Configuration.Document<Product>()
    .With(x => x.Name)
    .With(x => x.CategoryId);  // New projection

// HybridDb automatically re-projects all Product documents
```

### Migration Execution

Document migrations execute in two ways:

1. **On Load**: When documents are loaded via `session.Load()` or `session.Query()`, they are migrated immediately if needed
2. **Background**: Asynchronously in a separate task after store initialization completes

**Important**: Background migrations run continuously and asynchronously. You cannot know when they will complete, especially for large datasets. The background migration process:
- Starts after `store.Initialize()` completes
- Processes documents in batches (default 500 documents at a time)
- May take hours or days depending on dataset size
- Can be monitored via events but has no completion guarantee
- Documents are also migrated on-demand when loaded, so your application continues to work correctly even while background migrations are running

### Controlling Migrations

```csharp
// Disable background migrations
config.DisableBackgroundMigrations();

// Set batch size for background migrations
config.UseMigrationBatchSize(1000);  // Default is 500

// Enable upfront migrations on temp tables (for testing)
config.EnableUpfrontMigrationsOnTempTables();
```

### Migration Version Tracking

HybridDb tracks migration versions on each document:

```csharp
// Check if document needs migration
var awaitsReprojection = row["AwaitsReprojection"];
var version = row["Version"];
```

## Testing Migrations

### Test Migration Logic Directly

```csharp
[Fact]
public void MigrationAddsCategory()
{
    // Arrange: Create configuration and migration
    var configuration = new Configuration();
    var migration = new AddCategoryMigration();
    
    // Get the migration command
    var command = (ChangeDocument<Product>)migration.Background(configuration).Single();
    
    // Input JSON without Category field
    var inputJson = @"{""Id"":""prod-1"",""Name"":""Widget"",""Price"":19.99}";
    
    // Act: Execute the migration command
    var outputJson = command.Execute(new DefaultSerializer(), inputJson);
    
    // Assert: Category was added
    var doc = JObject.Parse(outputJson);
    doc.Value<string>("Category").ShouldBe("Uncategorized");
    doc.Value<string>("Name").ShouldBe("Widget");  // Other fields preserved
}

// The migration class
public class AddCategoryMigration(int version) : Migration(version)
{
    public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
    {
        yield return new ChangeDocument<Product>((serializer, json) =>
        {
            var doc = JObject.Parse(json);
            
            if (doc["Category"] == null || string.IsNullOrEmpty(doc.Value<string>("Category")))
            {
                doc["Category"] = "Uncategorized";
            }
            
            return doc.ToString();
        });
    }
}
```

### Test Schema Changes

```csharp
[Fact]
public void MigrationAddsIndex()
{
    var store = DocumentStore.ForTesting(TableMode.RealTables, config =>
    {
    config.Document<Product>()
        .With(x => x.CategoryId);
    
    config.UseMigrations(new AddIndexMigration());
    });
    
    store.Initialize();
    
    // Check that index exists
    var indexes = store.Database.RawQuery<string>(@"
    SELECT name FROM sys.indexes 
    WHERE object_id = OBJECT_ID('Products') 
    AND name = 'IX_Products_CategoryId'
    ");
    
    indexes.ShouldContain("IX_Products_CategoryId");
}
```

## Best Practices

### 1. Version Migrations Sequentially

Use descriptive class names and pass the version number when instantiating. This makes merge conflicts easier to resolve in team environments - when two developers create migrations simultaneously, you only need to change the version number in the instantiation rather than renaming the entire class.

```csharp
public class AddIndexes(int version) : Migration(version);
public class UpdateSchema(int version) : Migration(version);
public class RenameField(int version) : Migration(version);

// Usage:
config.UseMigrations(
    new AddIndexes(1),
    new UpdateSchema(2),
    new RenameField(3)
);
```

### 2. Never Modify Existing Migrations

Once deployed, don't change migration code. Create a new migration instead.
