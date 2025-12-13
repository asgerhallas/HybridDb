# Configuration - Connections and Testing

## Connection Strings

HybridDb uses standard SQL Server connection strings. The connection string is configured when creating the document store.

### Production Configuration

<!-- snippet: ProductionConfiguration -->
<a id='snippet-ProductionConfiguration'></a>

```cs
var newStore = DocumentStore.Create(config =>
{
    config.UseConnectionString(
        "Server=(LocalDb)\\MSSQLLocalDB;Database=MyAppDb;Integrated Security=True;Encrypt=False;");
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L22-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-ProductionConfiguration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Configuration Object

You can also configure settings using the Configuration object:

<!-- snippet: ConfigurationObject -->
<a id='snippet-ConfigurationObject'></a>

```cs
var configuration = new Configuration();
configuration.UseConnectionString("Server=(LocalDb)\\MSSQLLocalDB;Database=MyDb;Integrated Security=True;Encrypt=False;");

var newStore = DocumentStore.Create(configuration);
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L36-L41' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConfigurationObject' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Deferred Initialization

If you need to configure the store from multiple places before initializing:

<!-- snippet: DeferredInitialization -->
<a id='snippet-DeferredInitialization'></a>

```cs
var newStore = DocumentStore.Create(config =>
{
    config.UseConnectionString("Server=(LocalDb)\\MSSQLLocalDB;Database=MyDb;Integrated Security=True;Encrypt=False;");
}, initialize: false);

// Configure from multiple places
newStore.Configuration.Document<Product>().With(x => x.Name);

// Manually initialize when ready
newStore.Initialize();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L49-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-DeferredInitialization' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Testing Configurations

HybridDb provides special support for testing scenarios through the `ForTesting` factory method.

### Table Modes

HybridDb supports two table modes for testing:

#### TableMode.GlobalTempTables

Uses global temporary tables in TempDb. Best for test isolation:

<!-- snippet: GlobalTempTablesMode -->
<a id='snippet-GlobalTempTablesMode'></a>

```cs
var newStore = DocumentStore.ForTesting(
    TableMode.GlobalTempTables,
    config =>
    {
        config.UseConnectionString(
            "Server=(LocalDb)\\MSSQLLocalDB;Integrated Security=True;Encrypt=False;");
    });
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L68-L76' title='Snippet source file'>snippet source</a> | <a href='#snippet-GlobalTempTablesMode' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Characteristics**:
- Tables are created in TempDb
- Automatically cleaned up when the store is disposed
- Each store gets its own isolated set of tables
- Good for parallel test execution
- Requires TempDb access

#### TableMode.RealTables (with table name prefix)

Uses real tables with a prefix for isolation:

<!-- snippet: RealTablesMode -->
<a id='snippet-RealTablesMode'></a>

```cs
var newStore = DocumentStore.ForTesting(
    TableMode.RealTables,
    config =>
    {
        config.UseConnectionString(
            "Server=(LocalDb)\\MSSQLLocalDB;Database=HybridDb;Integrated Security=True;Encrypt=False;");
        
        // Add a unique prefix to avoid conflicts
        // If not set a default randomized prefix is used
        config.UseTableNamePrefix($"Test_{Guid.NewGuid():N}_");
    });

// Remember to clean up
newStore.Dispose();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L84-L99' title='Snippet source file'>snippet source</a> | <a href='#snippet-RealTablesMode' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Characteristics**:
- Tables persist in the database
- It will try to clean up, but may require manual cleanup if it fails
- Can be useful for debugging

## Configuration Options

### Logger

Configure logging to track what HybridDb is doing:

<!-- snippet: LoggerConfiguration -->
<a id='snippet-LoggerConfiguration'></a>

```cs
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
});

var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
{
    config.UseLogger(loggerFactory.CreateLogger("HybridDb"));
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L105-L115' title='Snippet source file'>snippet source</a> | <a href='#snippet-LoggerConfiguration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Serializer

HybridDb uses Newtonsoft.Json with a custom opinionated configuration called `DefaultSerializer`.

**DefaultSerializer Features:**

The `DefaultSerializer` is specifically designed for document storage with the following characteristics:

- **Field-based serialization**: Always serializes fields only, not properties. Auto properties are serialized by their hidden backing field.
- **Polymorphism support**: Special handling for polymorphic types and discriminators.
- **Reference tracking**: Handles parent and root references within JSON documents for performance and readable JSON documents.
- **Deterministic property order**: Orders properties in a specific, consistent manner.
- **Constructor bypass**: Bypasses constructors during deserialization to not run any logic at all.

**Configuring the Default Serializer:**

<!-- snippet: DefaultSerializerConfiguration -->
<a id='snippet-DefaultSerializerConfiguration'></a>

```cs
var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
{
    // Create and configure a custom serializer
    var serializer = new DefaultSerializer();
    serializer.EnableAutomaticBackReferences();
    
    config.UseSerializer(serializer);
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L123-L132' title='Snippet source file'>snippet source</a> | <a href='#snippet-DefaultSerializerConfiguration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Serializer Configuration Options:**

#### EnableAutomaticBackReferences()

Automatically handles parent/root references in your object graphs without duplicating data in the JSON:

```csharp
var serializer = new DefaultSerializer();
serializer.EnableAutomaticBackReferences();
```

When enabled:
- **Parent references** are serialized as simple string markers (`"parent"`, `"root"`) instead of duplicating entire objects
- **Automatic rehydration** during deserialization restores references to actual object instances
- **Significantly reduced document size** for object graphs with parent relationships
- **Clean, readable JSON** that is much easier to migrate than Json.NET's PreserveReferenceHandling (which adds `$ref` and `$id` metadata throughout the document, and places object definitions unpredictably - often not where you'd expect)

Example:
```csharp
public class Order
{
    public string Id { get; set; }
    public List<OrderLine> Lines { get; set; }
}

public class OrderLine
{
    public Order Order { get; set; }  // Back reference to parent
    public string ProductId { get; set; }
}

// With EnableAutomaticBackReferences:
// The OrderLine.Order property is stored as "parent" in JSON
// After deserialization, each OrderLine.Order points to the actual Order instance
```

You can also specify value types that should be duplicated instead of referenced:
```csharp
serializer.EnableAutomaticBackReferences(typeof(Product));
// Product instances will be duplicated even if referenced multiple times
```

#### EnableDiscriminators()

Adds type discriminators for polymorphic document storage:

```csharp
var serializer = new DefaultSerializer();
serializer.EnableDiscriminators(
    new Discriminator<PaymentMethod>("Payment"),
    new Discriminator<CreditCard>("Payment.CreditCard"),
    new Discriminator<BankTransfer>("Payment.BankTransfer")
);
```

When enabled:
- **Discriminator field** is added to JSON for polymorphic types
- **Type information** is preserved for correct deserialization of derived types
- **Explicit control** over discriminator values, unlike Json.NET's TypeNameHandling which:
  - Uses full namespace-qualified type names by default, making it difficult to refactor and move types around in your solution without migrations
  - Adds wrapper objects (e.g., `$type` and `$values` for lists), making JSON less readable and harder to migrate
- **Clean field placement** - Discriminator is always serialized first in the JSON, maintaining predictable structure

Example:
```csharp
// JSON output with discriminators:
{
    "Discriminator": "Payment.CreditCard",
    "CardNumber": "****-1234",
    "ExpiryDate": "12/25"
}

// Deserializes correctly to CreditCard type instead of base PaymentMethod
```

**Important:** Currently, HybridDb uses two separate discriminator systems:
- **Root documents** (configured via `config.Document<T>()`) use discriminators managed by the TypeMapper and stored in the database's Discriminator column
- **Child objects and lists** within the JSON document require `EnableDiscriminators()` to be called manually with explicit discriminator mappings

This means you need to call `EnableDiscriminators()` if your documents contain polymorphic child objects or collections. A future version may unify these systems to use the TypeMapper for all types.

**Custom Serializer:**

You can provide your own serializer by implementing the `ISerializer` interface:

```csharp
public class MyCustomSerializer : ISerializer
{
    // Implement serialization methods
}

// Use your custom serializer
config.UseSerializer(new MyCustomSerializer());
```

This allows you to use any serialization strategy that fits your needs, whether it's a different JSON library, protocol buffers, or any other format.

### Type Mapper

The type mapper controls how .NET types are mapped to discriminator strings:

```csharp
// Use short names (default)
config.UseTypeMapper(new ShortNameTypeMapper());

// Use custom type mapper
config.UseTypeMapper(new CustomTypeMapper());
```

### Table Name Prefix

Add a prefix to all table names:

<!-- snippet: TableNamePrefix -->
<a id='snippet-TableNamePrefix'></a>

```cs
var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
{
    config.UseTableNamePrefix("MyApp_");
    // Results in tables like: MyApp_Products, MyApp_Orders, etc.
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L140-L146' title='Snippet source file'>snippet source</a> | <a href='#snippet-TableNamePrefix' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Key Resolver

Customize how entity keys are resolved:

<!-- snippet: CustomKeyResolver -->
<a id='snippet-CustomKeyResolver'></a>

```cs
var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
{
    config.UseKeyResolver(entity =>
    {
        // Custom logic to get the key from an entity
        return entity.GetType()
            .GetProperty("Id")
            ?.GetValue(entity)
            ?.ToString();
    });
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L154-L166' title='Snippet source file'>snippet source</a> | <a href='#snippet-CustomKeyResolver' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Defaults to the Id property.

### Soft Delete

Enable soft deletes instead of hard deletes:

<!-- snippet: SoftDeleteConfiguration -->
<a id='snippet-SoftDeleteConfiguration'></a>

```cs
var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
{
    config.UseSoftDelete();
    // Deleted documents will have a metadata flag instead of being removed
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L174-L180' title='Snippet source file'>snippet source</a> | <a href='#snippet-SoftDeleteConfiguration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Background Migrations

Control background migration behavior:

<!-- snippet: BackgroundMigrationsConfiguration -->
<a id='snippet-BackgroundMigrationsConfiguration'></a>

```cs
var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
{
    // Disable background migrations (migrations only run on document load)
    config.DisableBackgroundMigrations();

    // Set migration batch size
    config.UseMigrationBatchSize(1000);
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L188-L197' title='Snippet source file'>snippet source</a> | <a href='#snippet-BackgroundMigrationsConfiguration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Event Store

Enable the event store feature:

<!-- snippet: EventStoreConfiguration -->
<a id='snippet-EventStoreConfiguration'></a>

```cs
var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
{
    config.UseEventStore();
});
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc03_ConfigurationTests.cs#L205-L210' title='Snippet source file'>snippet source</a> | <a href='#snippet-EventStoreConfiguration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->