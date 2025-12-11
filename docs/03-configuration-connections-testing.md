# Configuration - Connections and Testing

## Connection Strings

HybridDb uses standard SQL Server connection strings. The connection string is configured when creating the document store.

### Production Configuration

```csharp
var store = DocumentStore.Create(config =>
{
    config.UseConnectionString(
        "Server=myserver;Database=MyAppDb;User Id=myuser;Password=mypass;Encrypt=False;");
});

store.Initialize();
```

### Connection String Components

**Server/Data Source**:
```csharp
// Local instance
"Server=localhost;..."
"Server=.;..."
"Data Source=(local);..."

// Named instance
"Server=localhost\\SQLEXPRESS;..."

// Remote server
"Server=myserver.domain.com;..."
```

**Authentication**:
```csharp
// Windows Authentication (Integrated Security)
"Server=.;Database=MyDb;Integrated Security=True;Encrypt=False;"

// SQL Server Authentication
"Server=.;Database=MyDb;User Id=sa;Password=mypass;Encrypt=False;"

// Azure SQL Database
"Server=myserver.database.windows.net;Database=MyDb;User Id=myuser;Password=mypass;Encrypt=True;"
```

**Additional Options**:
```csharp
// Connection timeout
"Server=.;Database=MyDb;Integrated Security=True;Connection Timeout=30;Encrypt=False;"

// Application name (useful for monitoring)
"Server=.;Database=MyDb;Integrated Security=True;Application Name=MyApp;Encrypt=False;"

// Multiple Active Result Sets
"Server=.;Database=MyDb;Integrated Security=True;MultipleActiveResultSets=True;Encrypt=False;"
```

### Configuration Object

You can also configure settings using the Configuration object:

```csharp
var configuration = new Configuration();
configuration.UseConnectionString("Server=.;Database=MyDb;Integrated Security=True;Encrypt=False;");

var store = DocumentStore.Create(configuration);
```

### Deferred Initialization

If you need to configure the store from multiple places before initializing:

```csharp
var store = DocumentStore.Create(config =>
{
    config.UseConnectionString("Server=.;Database=MyDb;Integrated Security=True;Encrypt=False;");
}, initialize: false);

// Configure from multiple places
store.Configuration.Document<Product>().With(x => x.Name);
SomeOtherConfigurationMethod(store.Configuration);

// Manually initialize when ready
store.Initialize();
```

## Testing Configurations

HybridDb provides special support for testing scenarios through the `ForTesting` factory method.

### Table Modes

HybridDb supports two table modes for testing:

#### TableMode.GlobalTempTables

Uses global temporary tables in TempDb. Best for test isolation:

```csharp
var store = DocumentStore.ForTesting(
    TableMode.GlobalTempTables,
    config =>
    {
        config.UseConnectionString(
            "Server=.;Integrated Security=True;Encrypt=False;");
    });

store.Initialize();
```

**Characteristics**:
- Tables are created in TempDb
- Automatically cleaned up when the store is disposed
- Each store gets its own isolated set of tables
- Good for parallel test execution
- Requires TempDb access

#### TableMode.RealTables (with table name prefix)

Uses real tables with a prefix for isolation:

```csharp
var store = DocumentStore.ForTesting(
    TableMode.RealTables,
    config =>
    {
        config.UseConnectionString(
            "Server=.;Database=TestDb;Integrated Security=True;Encrypt=False;");
        
        // Add a unique prefix to avoid conflicts
        config.UseTableNamePrefix($"Test_{Guid.NewGuid():N}_");
    });

store.Initialize();

// Remember to clean up
store.Dispose();
```

**Characteristics**:
- Tables persist in the database
- Requires manual cleanup
- Can be useful for debugging
- Allows inspection of data after tests

### Test Base Class Pattern

Create a base class for your tests:

```csharp
public abstract class HybridDbTestBase : IDisposable
{
    protected DocumentStore Store { get; }
    
    protected HybridDbTestBase()
    {
        Store = DocumentStore.ForTesting(
            TableMode.GlobalTempTables,
            config =>
            {
                config.UseConnectionString(
                    "Server=.;Integrated Security=True;Encrypt=False;");
                
                // Configure documents
                Configure(config);
            });
        
        Store.Initialize();
    }
    
    protected virtual void Configure(Configuration config)
    {
        // Override in derived classes
    }
    
    protected DocumentDesigner<T> Document<T>(string tablename = null) 
        => Store.Configuration.Document<T>(tablename);
    
    public void Dispose()
    {
        Store?.Dispose();

// Usage in tests
public class MyTests : HybridDbTestBase
{
    protected override void Configure(Configuration config)
    {
        config.Document<MyEntity>()
            .With(x => x.Name);
    }
    
    [Fact]
    public void MyTest()
    {
        using var session = Store.OpenSession();
        // ... test code
```

### In-Memory Testing Alternative

For true in-memory testing, consider using a separate test database that you can quickly reset:

```csharp
public class TestDatabaseFixture : IDisposable
{
    public DocumentStore Store { get; }
    
    public TestDatabaseFixture()
    {
        // Create a unique test database
        var dbName = $"HybridDbTests_{Guid.NewGuid():N}";
        var masterConnection = "Server=.;Database=master;Integrated Security=True;Encrypt=False;";
        
        using (var connection = new SqlConnection(masterConnection))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{dbName}]";
            cmd.ExecuteNonQuery();
        }
        
        Store = DocumentStore.Create(config =>
        {
            config.UseConnectionString(
                $"Server=.;Database={dbName};Integrated Security=True;Encrypt=False;");
        });
        
        Store.Initialize();
    }
    
    public void Dispose()
    {
        var dbName = Store.Database.ConnectionString
            .Split(';')
            .First(x => x.StartsWith("Database="))
            .Split('=')[1];
        
        Store.Dispose();
        
        // Drop the test database
        var masterConnection = "Server=.;Database=master;Integrated Security=True;Encrypt=False;";
        using var connection = new SqlConnection(masterConnection);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DROP DATABASE [{dbName}]";
        cmd.ExecuteNonQuery();
```

## Configuration Options

### Logger

Configure logging to track what HybridDb is doing:

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

config.UseLogger(loggerFactory.CreateLogger("HybridDb"));
```

### Serializer

HybridDb uses Newtonsoft.Json with a custom opinionated configuration called `DefaultSerializer`.

**DefaultSerializer Features:**

The `DefaultSerializer` is specifically designed for document storage with the following characteristics:

- **Field-based serialization**: Always serializes fields only, not properties. Auto properties are serialized by their hidden backing field.
- **Polymorphism support**: Special handling for polymorphic types and discriminators.
- **Reference tracking**: Handles parent and root references within JSON documents for performance and readable JSON documents.
- **Deterministic property order**: Orders properties in a specific, consistent manner.
- **Constructor bypass**: Bypasses constructors during deserialization to not run any logic at all.

**Using the Default Serializer:**

```csharp
// Configure the default serializer with options
var serializer = config.UseDefaultSerializer();
serializer.EnableAutomaticBackReferences();
```

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

```csharp
config.UseTableNamePrefix("MyApp_");
// Results in tables like: MyApp_Products, MyApp_Orders, etc.
```

### Key Resolver

Customize how entity keys are resolved:

```csharp
config.UseKeyResolver(entity =>
{
    // Custom logic to get the key from an entity
    if (entity is IHasCustomKey custom)
        return custom.GetKey();
    
    // Fallback to default
    return entity.GetType()
        .GetProperty("Id")
        ?.GetValue(entity)
        ?.ToString();
});
```

### Soft Delete

Enable soft deletes instead of hard deletes:

```csharp
config.UseSoftDelete();

// Deleted documents will have a metadata flag instead of being removed
```

### Background Migrations

Control background migration behavior:

```csharp
// Disable background migrations (migrations only run on document load)
config.DisableBackgroundMigrations();

// Set migration batch size
config.UseMigrationBatchSize(1000);
```

### Event Store

Enable the event store feature:

```csharp
config.UseEventStore();
```

## Environment-Specific Configuration

### Development

```csharp
var store = DocumentStore.Create(config =>
{
    config.UseConnectionString(
        "Server=localhost;Database=MyApp_Dev;Integrated Security=True;Encrypt=False;");
    
    var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    config.UseLogger(loggerFactory.CreateLogger("HybridDb"));
});
```

### Staging

```csharp
var store = DocumentStore.Create(config =>
{
    config.UseConnectionString(
        Environment.GetEnvironmentVariable("HYBRIDDB_CONNECTION_STRING"));
    
    config.UseTableNamePrefix("Staging_");
});
```

### Production

```csharp
var store = DocumentStore.Create(config =>
{
    config.UseConnectionString(
        Environment.GetEnvironmentVariable("HYBRIDDB_CONNECTION_STRING"));
    
    // Production-specific settings
    config.UseMigrationBatchSize(100);
    
    var loggerFactory = LoggerFactory.Create(b => 
    {
        b.AddApplicationInsights();
        b.SetMinimumLevel(LogLevel.Warning);
    });
    config.UseLogger(loggerFactory.CreateLogger("HybridDb"));
});
```

## Connection Pooling

HybridDb relies on SQL Server's built-in connection pooling. To configure pool settings:

```csharp
config.UseConnectionString(
    "Server=.;Database=MyDb;Integrated Security=True;" +
    "Min Pool Size=5;Max Pool Size=100;" +
    "Connection Lifetime=300;Encrypt=False;");
```

## Troubleshooting

### Connection Failures

If connections fail, check:

1. SQL Server is running
2. Server name is correct
3. Database exists (for real tables)
4. User has appropriate permissions
5. Firewall allows connections
6. `Encrypt=False` is set for local development

### Permission Issues

Ensure the database user has these permissions:
- CREATE TABLE
- ALTER TABLE
- SELECT, INSERT, UPDATE, DELETE
- CREATE INDEX

For TempDb mode:
- CREATE TABLE in TempDb

### TempDb Limitations

Global temp tables have some limitations:
- Name length restrictions
- Can't use certain SQL features
- May have performance implications for large datasets

Consider using real tables with a prefix for complex testing scenarios.
