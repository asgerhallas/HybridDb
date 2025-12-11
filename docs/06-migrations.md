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

1. **Incompatible Model Evolution**: Your C# classes may have changed in ways that make old JSON impossible to deserialize
   - Added required properties that old documents don't have
   - Changed property types (e.g., `int` → `string`, `DateTime` → `DateTimeOffset`)
   - Removed properties that old JSON still contains
   
2. **Future-Proofing**: Code that works today may break tomorrow
   - Next year's model changes could make this migration fail retroactively
   - You can't re-run failed migrations if the models have evolved
   
3. **Serialization Dependencies**: Deserialization requires compatible types
   - Missing assemblies or type renames cause failures
   - Custom converters may not handle old data
   
4. **Version Skew**: A document from version 1 being migrated to version 5 may not deserialize with version 5's model

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
- **Reliable**: Handles any JSON structure

## Migration Basics

Migrations are defined by implementing the `Migration` class:

```csharp
public class MyMigration : Migration
{
    public MyMigration() : base(version: 1) { }
    
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
    }
}
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
        new Migration001_AddIndexes(),
        new Migration002_UpdateProductSchema(),
        new Migration003_RenameField()
    );
});

store.Initialize();
```

### Migration Versioning

Migrations are versioned and run in order:

```csharp
public class Migration001_AddIndexes : Migration
{
    public Migration001_AddIndexes() : base(1) { }
    // ...
}

public class Migration002_UpdateSchema : Migration
{
    public Migration002_UpdateSchema() : base(2) { }
    // ...
}
```

HybridDb tracks which migrations have been run in the metadata table and only runs new ones.

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
}, initialize: false);

store.Initialize();

// Later: Add Price column - HybridDb will automatically add it
store.Configuration.Document<Product>()
    .With(x => x.Price);  // New column

store.Initialize();  // Run again to apply schema changes
```

HybridDb detects the difference and creates the `Price` column automatically.

### Before Auto Migrations

Run commands before automatic schema migrations:

```csharp
public class AddDefaultPriceColumn : Migration
{
    public AddDefaultPriceColumn() : base(1) { }
    
    public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
    {
        var table = configuration.GetDesignFor<Product>().Table;
        
        // Add column with default value before auto-migration
        yield return new AddColumn(
            table.Name, 
            new Column("Price", typeof(decimal), defaultValue: 0m)
        );
    }
}
```

**Use case**: Add columns with default values to avoid triggering re-projection.

### After Auto Migrations

Run commands after automatic schema migrations:

```csharp
public class AddIndexes : Migration
{
    public AddIndexes() : base(1) { }
    
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
    {
        var table = configuration.GetDesignFor<Product>().Table;
        
        // Add indexes after columns are created
        yield return new RawSqlCommand($@"
            CREATE INDEX IX_Products_CategoryId 
            ON {table.Name} (CategoryId)
        ");
        
        yield return new RawSqlCommand($@"
            CREATE INDEX IX_Products_Price 
            ON {table.Name} (Price) 
            WHERE Price > 0
        ");
    }
}
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
var table = configuration.GetDesignFor<Product>().Table;
yield return new RenameColumn(table, "OldName", "NewName");

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
public class UpdateProductSchema : Migration
{
    public UpdateProductSchema() : base(2) { }
    
    public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
    {
        yield return new ChangeDocument<Product>((serializer, json) =>
        {
            // IMPORTANT: Migrate the JSON directly, not deserialized models
            // Models may have changed in incompatible ways or may change in the future
            var doc = JObject.Parse(json);
            
            // Add missing category
            if (doc["Category"] == null || string.IsNullOrEmpty((string)doc["Category"]))
            {
                doc["Category"] = "Uncategorized";
            }
            
            // Fix negative prices
            if (doc["Price"] != null && (decimal)doc["Price"] < 0)
            {
                doc["Price"] = 0;
            }
            
            return doc.ToString();
        });
    }
}
```

### ChangeDocument - JSON-Based Migrations

**CRITICAL: Always migrate JSON directly, not deserialized C# objects.**

The `ChangeDocument<T>` class is the primary way to migrate documents. It works with JSON strings directly:

```csharp
yield return new ChangeDocument<Product>((serializer, json) =>
{
    // Migrate JSON directly - models may have changed incompatibly
    var doc = JObject.Parse(json);
    
    // Transform the JSON structure
    doc["UpdatedField"] = ((string)doc["OldField"])?.ToUpper();
    
    return doc.ToString();
});
```

**Why JSON-Based Migrations?**

1. **Model Evolution**: Your C# classes may have changed in ways that make old serialized data impossible to deserialize
2. **Future Safety**: Models that migrate successfully today may fail when classes evolve in the future
3. **Serialization Independence**: JSON manipulation doesn't depend on current class structure
4. **Type Safety**: Avoid runtime errors from incompatible type conversions

**Advanced Usage with Session Access:**

```csharp
yield return new ChangeDocument<Product>((session, serializer, row, json) =>
{
    var doc = JObject.Parse(json);
    
    // Access metadata from row dictionary
    var id = row["Id"];
    var etag = row["Etag"];
    
    // Use session for complex operations
    doc["MigratedAt"] = DateTime.UtcNow;
    
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

### Controlling Background Migrations

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

## Advanced Migration Scenarios

### Conditional Migrations

Migrate only specific documents by checking JSON content:

```csharp
public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
{
    yield return new ChangeDocument<Product>((serializer, json) =>
    {
        var doc = JObject.Parse(json);
        
        // Only migrate products without a category
        if (doc["Category"] == null || string.IsNullOrEmpty((string)doc["Category"]))
        {
            doc["Category"] = DetermineCategory(doc);
        }
        
        return doc.ToString();
    });
}
```

### Multi-Step Migrations

Break complex migrations into steps. Note that `BeforeAutoMigrations` and `AfterAutoMigrations` run synchronously during store initialization, while `Background` migrations run asynchronously and you cannot know when they complete:

```csharp
public class Migration003_RestructureProducts : Migration
{
    public Migration003_RestructureProducts() : base(3) { }
    
    public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
    {
        // Runs first during store initialization
        // Add new columns with defaults
        yield return new AddColumn("Products", 
            new Column("Category", typeof(string), defaultValue: "General"));
    }
    
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
    {
        // Runs second during store initialization (after auto migrations)
        // Add constraints/indexes
        yield return new RawSqlCommand(@"
            CREATE INDEX IX_Products_Category 
            ON Products (Category)
        ");
    }
    
    public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
    {
        // Runs ASYNCHRONOUSLY in the background after store initialization
        // May take hours/days to complete on large datasets
        // Documents are also migrated on-load if not yet migrated
        yield return new ChangeDocument<Product>((serializer, json) =>
        {
            var doc = JObject.Parse(json);
            doc["Category"] = MapOldCategoryToNew((string)doc["OldCategory"]);
            return doc.ToString();
        });
    }
}
```

### Renaming Document Properties

When renaming properties, migrate the JSON structure:

```csharp
public class Product
{
    public string Id { get; set; }
    public string ProductName { get; set; }  // Was "Name"
}

public class RenameProductName : Migration
{
    public RenameProductName() : base(4) { }
    
    public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
    {
        yield return new ChangeDocument<Product>((serializer, json) =>
        {
            var doc = JObject.Parse(json);
            
            // Rename "Name" to "ProductName" in JSON
            if (doc["Name"] != null)
            {
                doc["ProductName"] = doc["Name"];
                doc.Remove("Name");
            }
            
            return doc.ToString();
        });
    }
}
```

### Splitting Documents

Split one document type into multiple by working with JSON:

```csharp
public class SplitOrdersAndInvoices : Migration
{
    public SplitOrdersAndInvoices() : base(5) { }
    
    public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
    {
        yield return new ChangeDocument<OldOrder>((session, serializer, row, json) =>
        {
            var oldDoc = JObject.Parse(json);
            
            // Create new Order JSON structure
            var orderDoc = new JObject
            {
                ["Id"] = oldDoc["Id"],
                ["CustomerId"] = oldDoc["CustomerId"],
                ["Items"] = oldDoc["Items"]
            };
            
            var order = serializer.Deserialize<Order>(orderDoc.ToString());
            session.Store(order);
            
            // Create separate Invoice from embedded data
            if (oldDoc["InvoiceData"] != null)
            {
                var invoiceDoc = new JObject
                {
                    ["Id"] = Guid.NewGuid().ToString(),
                    ["OrderId"] = oldDoc["Id"],
                    ["Amount"] = oldDoc["InvoiceData"]["Amount"],
                    ["DueDate"] = oldDoc["InvoiceData"]["DueDate"]
                };
                
                var invoice = serializer.Deserialize<Invoice>(invoiceDoc.ToString());
                session.Store(invoice);
            }
            
            return orderDoc.ToString();
        });
    }
}
```

### Merging Documents

Merge multiple documents into one using JSON:

```csharp
public class MergeUserAndProfile : Migration
{
    public MergeUserAndProfile() : base(6) { }
    
    public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
    {
        yield return new ChangeDocument<User>((session, serializer, row, json) =>
        {
            var userDoc = JObject.Parse(json);
            
            // Load related profile
            var profileId = $"profile-{userDoc["Id"]}";
            var profile = session.Load<OldProfile>(profileId);
            
            if (profile != null)
            {
                // Merge profile data into user JSON
                var profileJson = serializer.Serialize(profile);
                var profileDoc = JObject.Parse(profileJson);
                
                userDoc["Bio"] = profileDoc["Bio"];
                userDoc["Avatar"] = profileDoc["Avatar"];
                userDoc["Preferences"] = profileDoc["Preferences"];
                
                // Delete old profile
                session.Delete(profile);
            }
            
            return userDoc.ToString();
        });
    }
}
```

## Migration Patterns

### 1. Add Column with Default Value

```csharp
public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
{
    yield return new AddColumn("Products", 
        new Column("Status", typeof(string), defaultValue: "Active"));
}
```

### 2. Backfill Data

```csharp
public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
{
    yield return new RawSqlCommand(@"
        UPDATE Products 
        SET Category = 'General' 
        WHERE Category IS NULL OR Category = ''
    ");
}
```

### 3. Remove Obsolete Column

```csharp
public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
{
    // First ensure no code references the column
    // Then remove it in a migration
    yield return new RemoveColumn("Products", "ObsoleteField");
}
```

### 4. Change Column Type

```csharp
public class ChangePriceToDecimal : Migration
{
    public ChangePriceToDecimal() : base(7) { }
    
    public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
    {
        // Add new column
        yield return new AddColumn("Products", 
            new Column("PriceDecimal", typeof(decimal)));
        
        // Copy data
        yield return new RawSqlCommand(@"
            UPDATE Products 
            SET PriceDecimal = CAST(Price AS decimal(18,2))
        ");
    }
    
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
    {
        // Remove old column
        yield return new RemoveColumn("Products", "Price");
        
        // Rename new column
        var table = configuration.GetDesignFor<Product>().Table;
        yield return new RenameColumn(table, "PriceDecimal", "Price");
    }
}
```

## Testing Migrations

### Test Migration Execution

```csharp
[Fact]
public void MigrationAddsCategory()
{
    // Arrange: Create store with version 1
    var store = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
    {
        config.Document<Product>()
            .With(x => x.Name);
        
        config.UseMigrations(new AddCategoryMigration());
    });
    
    store.Initialize();
    
    // Act: Store a product
    using (var session = store.OpenSession())
    {
        session.Store(new Product { Id = "prod-1", Name = "Widget" });
        session.SaveChanges();
    }
    
    // Assert: Category was added by migration
    using (var session = store.OpenSession())
    {
        var product = session.Load<Product>("prod-1");
        product.Category.ShouldBe("Uncategorized");
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

```csharp
public class Migration001 : Migration { public Migration001() : base(1) { } }
public class Migration002 : Migration { public Migration002() : base(2) { } }
public class Migration003 : Migration { public Migration003() : base(3) { } }
```

### 2. Never Modify Existing Migrations

Once deployed, don't change migration code. Create a new migration instead.

### 3. Test Migrations Thoroughly

Test both upgrade paths and rollback scenarios.

### 4. Use Descriptive Migration Names

```csharp
public class Migration005_AddProductCategories : Migration { }
public class Migration006_SplitOrdersAndInvoices : Migration { }
```

### 5. Document Breaking Changes

```csharp
/// <summary>
/// BREAKING: Renames Product.Name to Product.ProductName
/// Requires code changes before deployment
/// </summary>
public class Migration010_RenameProductName : Migration { }
```

### 6. Handle Large Data Sets

For large migrations, use batching:

```csharp
config.UseMigrationBatchSize(100);  // Process 100 documents at a time
```

### 7. Monitor Background Migrations

```csharp
config.AddEventHandler(@event =>
{
    if (@event is MigrationStarted started)
    {
        logger.LogInformation("Background migration started");
    }
    
    if (@event is MigrationEnded ended)
    {
        logger.LogInformation("Background migration completed");
    }
});
```

## Troubleshooting

### Migration Not Running

Check that:
1. Store is initialized: `store.Initialize()`
2. Migration is registered: `config.UseMigrations(migration)`
3. Version number is higher than current schema version
4. Background migrations are enabled

### Migration Fails Partway

Migrations run in transactions. If a migration fails:
- Schema migrations: Transaction is rolled back
- Document migrations: Failed batch is skipped, continues with next batch

### Performance Issues

For large migrations:
- Increase batch size: `config.UseMigrationBatchSize(1000)`
- Run during off-peak hours
- Consider disabling background migrations and running manually
- Add indexes to speed up queries

### Concurrency Issues

Background migrations use `ROWLOCK, UPDLOCK, READPAST`:
- Documents being migrated are locked
- Other processes skip locked documents
- No blocking on reads

## Migration Timeline Example

```csharp
// Version 1: Initial release
public class Migration001_Initial : Migration
{
    public Migration001_Initial() : base(1) { }
    
    public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration config)
    {
        yield return new RawSqlCommand("CREATE INDEX IX_Products_Name ON Products (Name)");
    }
}

// Version 2: Add categories
public class Migration002_AddCategories : Migration
{
    public Migration002_AddCategories() : base(2) { }
    
    public override IEnumerable<RowMigrationCommand> Background(Configuration config)
    {
        yield return new ChangeDocument<Product>((serializer, json) =>
        {
            var doc = JObject.Parse(json);
            if (doc["Category"] == null)
            {
                doc["Category"] = "General";
            }
            return doc.ToString();
        });
    }
}

// Version 3: Split into subcategories
public class Migration003_AddSubcategories : Migration
{
    public Migration003_AddSubcategories() : base(3) { }
    
    public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration config)
    {
        yield return new AddColumn("Products", 
            new Column("Subcategory", typeof(string), defaultValue: ""));
    }
}
```
