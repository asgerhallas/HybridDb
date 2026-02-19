# Documentation Code Samples

This directory contains xUnit tests that validate all code samples used in the HybridDb documentation.

## Overview

All code samples in the documentation are extracted from these test files to ensure they:
- **Compile correctly** - Tests must build successfully
- **Run correctly** - Tests must pass
- **Stay up-to-date** - Documentation automatically reflects code changes

## Structure

Each documentation file has a corresponding test file:

- `Doc01_GettingStartedTests.cs` → `docs/01-getting-started.md`
- `Doc02_HowToBuildTests.cs` → `docs/02-how-to-build-and-contribute.md`
- `Doc03_ConnectionsTests.cs` → `docs/03-configuration-connections-testing.md`
- `Doc04_DocumentsTests.cs` → `docs/04-configuration-documents-tables-projections.md`
- `Doc05_EventsTests.cs` → `docs/05-configuration-events.md`
- `Doc06_MigrationsTests.cs` → `docs/06-migrations.md`
- `Doc07_TransactionTests.cs` → `docs/07-documentstore-transaction.md`
- `Doc08_StoreLoadTests.cs` → `docs/08-documentsession-store-load.md`
- `Doc09_QueryTests.cs` → `docs/09-documentsession-query.md`
- `Doc10_AdvancedTests.cs` → `docs/10-documentsession-advanced.md`

## How It Works

### 1. Code Regions in Tests

Code samples are marked with `#region` / `#endregion` comments:

```csharp
[Fact]
public void QuickStart_BasicExample()
{
    // #region QuickStart_BasicExample
    var store = DocumentStore.ForTesting(TableMode.GlobalTempTables);
    store.Document<Entity>().With(x => x.Property);
    
    using var session = store.OpenSession();
    session.Store(new Entity { Id = Guid.NewGuid(), Property = "Hello" });
    session.SaveChanges();
    // #endregion
    
    // Assertions and additional test code...
}
```

### 2. Markdown References

In the markdown files, reference the code region:

```markdown
## Quick Start

<!-- embed:Doc01_GettingStartedTests#QuickStart_BasicExample -->
\```csharp
var store = DocumentStore.ForTesting(TableMode.GlobalTempTables);
store.Document<Entity>().With(x => x.Property);

using var session = store.OpenSession();
session.Store(new Entity { Id = Guid.NewGuid(), Property = "Hello" });
session.SaveChanges();
\```
<!-- /embed -->

### 3. Extract and Embed

Run the embedding script to update markdown files:

```bash
pwsh embed-code-samples.ps1
```

This extracts all code regions from test files and can update the markdown files.

## Adding New Code Samples

1. **Add a test** in the appropriate `Doc##_*Tests.cs` file:
   ```csharp
   [Fact]
   public void MyNewExample()
   {
       // #region MyNewExample
       var store = DocumentStore.Create(...);
       // ... your code ...
       // #endregion
       
       // Add assertions
       result.ShouldNotBeNull();
   }
   ```

2. **Run the tests** to ensure the code compiles and works:
   ```bash
   dotnet test
   ```

3. **Reference in markdown**:
   ```markdown
   <!-- embed:Doc01_GettingStartedTests#MyNewExample -->
   \```csharp
   var store = DocumentStore.Create(...);
   // ... your code ...
   \```
   <!-- /embed -->
   ```

4. **Run embed script**:
   ```bash
   pwsh embed-code-samples.ps1
   ```

## Benefits

✅ **Guaranteed Correctness** - All code samples must compile  
✅ **Automated Testing** - Code samples are tested on every build  
✅ **Easy Updates** - Update code in one place, reflected everywhere  
✅ **Prevents Drift** - Documentation can't get out of sync with code  
✅ **IntelliSense Support** - Write code samples with full IDE support  

## Running Tests

Run all documentation tests:

```bash
dotnet test --filter "FullyQualifiedName~Documentation"
```

Run tests for a specific documentation file:

```bash
dotnet test --filter "FullyQualifiedName~Doc01"
```

## Common Patterns

### Simple Example
```csharp
[Fact]
public void SimpleExample()
{
    // #region SimpleExample
    using var session = store.OpenSession();
    var product = session.Load<Product>("p1");
    // #endregion
}
```

### Example with Setup
```csharp
[Fact]
public void ExampleWithSetup()
{
    // Setup
    var store = DocumentStore.ForTesting(TableMode.GlobalTempTables);
    store.Document<Product>();
    
    // #region ExampleWithSetup
    using var session = store.OpenSession();
    session.Store(new Product { Id = "p1", Name = "Widget" });
    session.SaveChanges();
    // #endregion
    
    // Assertions
    session.Query<Product>().Count().ShouldBe(1);
}
```

### Multiple Regions in One Test
```csharp
[Fact]
public void MultipleRegions()
{
    // #region Setup
    var store = DocumentStore.ForTesting(TableMode.GlobalTempTables);
    // #endregion
    
    // #region Usage
    using var session = store.OpenSession();
    session.Store(new Product { Id = "p1" });
    // #endregion
}
```

## Future Enhancements

- Automatic markdown updating on build
- VS Code extension for live preview
- CI/CD validation that docs are in sync
- Auto-generation of missing regions
