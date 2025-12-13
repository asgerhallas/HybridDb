# DocumentSession - Query

## Overview

HybridDb provides powerful querying capabilities through LINQ and SQL. You can query documents using projected columns for efficient database queries while still working with full document objects.

## LINQ Queries

### Basic LINQ Query

<!-- snippet: BasicLINQQuery -->
<a id='snippet-BasicLINQQuery'></a>

```cs
using var session = store.OpenSession();

var products = session.Query<Product>()
    .Where(x => x.Price > 100)
    .ToList();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L42-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-BasicLINQQuery' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Query All Documents

<!-- snippet: QueryAllDocuments -->
<a id='snippet-QueryAllDocuments'></a>

```cs
using var session = store.OpenSession();

var allProducts = session.Query<Product>().ToList();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L58-L62' title='Snippet source file'>snippet source</a> | <a href='#snippet-QueryAllDocuments' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Query with Single Result

<!-- snippet: QueryWithSingleResult -->
<a id='snippet-QueryWithSingleResult'></a>

```cs
using var session = store.OpenSession();

var product = session.Query<Product>()
    .Where(x => x.Name == "Widget")
    .SingleOrDefault();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L72-L78' title='Snippet source file'>snippet source</a> | <a href='#snippet-QueryWithSingleResult' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Query with First

<!-- snippet: QueryWithFirst -->
<a id='snippet-QueryWithFirst'></a>

```cs
using var session = store.OpenSession();

var product = session.Query<Product>()
    .Where(x => x.Price > 50)
    .FirstOrDefault();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L88-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-QueryWithFirst' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Where Clauses

### Equality

<!-- snippet: WhereEquality -->
<a id='snippet-WhereEquality'></a>

```cs
var session = store.OpenSession();

var products = session.Query<Product>()
    .Where(x => x.CategoryId == "electronics")
    .ToList();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L104-L110' title='Snippet source file'>snippet source</a> | <a href='#snippet-WhereEquality' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Comparison Operators

<!-- snippet: ComparisonOperators -->
<a id='snippet-ComparisonOperators'></a>

```cs
// Greater than
var expensive = session.Query<Product>()
    .Where(x => x.Price > 1000)
    .ToList();

// Less than or equal
var affordable = session.Query<Product>()
    .Where(x => x.Price <= 50)
    .ToList();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L122-L132' title='Snippet source file'>snippet source</a> | <a href='#snippet-ComparisonOperators' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Null Checks

```csharp
// Is null
var noCategory = session.Query<Product>()
    .Where(x => x.CategoryId == null)
    .ToList();

// Is not null
var hasCategory = session.Query<Product>()
    .Where(x => x.CategoryId != null)
    .ToList();

### Logical Operators

<!-- snippet: LogicalOperators -->
<a id='snippet-LogicalOperators'></a>

```cs
// AND
var filtered = session.Query<Product>()
    .Where(x => x.Price > 100 && x.CategoryId == "electronics")
    .ToList();

// OR
var multiple = session.Query<Product>()
    .Where(x => x.CategoryId == "electronics" || x.CategoryId == "computers")
    .ToList();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L145-L155' title='Snippet source file'>snippet source</a> | <a href='#snippet-LogicalOperators' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Multiple Where Clauses

<!-- snippet: MultipleWhereClauses -->
<a id='snippet-MultipleWhereClauses'></a>

```cs
var products = session.Query<Product>()
    .Where(x => x.Price > 50)
    .Where(x => x.CategoryId == "electronics")
    .Where(x => x.Stock > 0)
    .ToList();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L168-L174' title='Snippet source file'>snippet source</a> | <a href='#snippet-MultipleWhereClauses' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## String Operations

<!-- snippet: StringOperations -->
<a id='snippet-StringOperations'></a>

```cs
// StartsWith
var startsWithPro = session.Query<Product>()
    .Where(x => x.Name.StartsWith("Pro"))
    .ToList();

// Contains
var containsWidget = session.Query<Product>()
    .Where(x => x.Name.Contains("Widget"))
    .ToList();

// EndsWith
var endsWith2000 = session.Query<Product>()
    .Where(x => x.Name.EndsWith("2000"))
    .ToList();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L186-L201' title='Snippet source file'>snippet source</a> | <a href='#snippet-StringOperations' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Query with Variables

### Local Variables

```csharp
var minPrice = 100m;
var category = "electronics";

var products = session.Query<Product>()
    .Where(x => x.Price > minPrice && x.CategoryId == category)
    .ToList();
```

### Nested Properties

```csharp
var filter = new { MinPrice = 100m, Category = "electronics" };

var products = session.Query<Product>()
    .Where(x => x.Price > filter.MinPrice)
    .ToList();
```

## Ordering

<!-- snippet: OrderingResults -->
<a id='snippet-OrderingResults'></a>

```cs
// Order by ascending
var byPrice = session.Query<Product>()
    .OrderBy(x => x.Price)
    .ToList();

// Order by descending
var byPriceDesc = session.Query<Product>()
    .OrderByDescending(x => x.Price)
    .ToList();

// Multiple orderings
var multiOrder = session.Query<Product>()
    .OrderBy(x => x.CategoryId)
    .ThenByDescending(x => x.Price)
    .ToList();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L213-L229' title='Snippet source file'>snippet source</a> | <a href='#snippet-OrderingResults' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Pagination

<!-- snippet: TakeAndSkip -->
<a id='snippet-TakeAndSkip'></a>

```cs
// Take first 10
var firstPage = session.Query<Product>()
    .OrderBy(x => x.Name)
    .Take(10)
    .ToList();

// Skip and take (pagination)
var secondPage = session.Query<Product>()
    .OrderBy(x => x.Name)
    .Skip(10)
    .Take(10)
    .ToList();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L242-L255' title='Snippet source file'>snippet source</a> | <a href='#snippet-TakeAndSkip' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Getting Total Count

<!-- snippet: CountQueries -->
<a id='snippet-CountQueries'></a>

```cs
// Count all
var totalCount = session.Query<Product>().Count();

// Count with filter
var expensiveCount = session.Query<Product>()
    .Where(x => x.Price > 100)
    .Count();

// Any
var hasProducts = session.Query<Product>().Any();

// Any with filter
var hasExpensive = session.Query<Product>()
    .Where(x => x.Price > 1000)
    .Any();
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc09_QueryTests.cs#L267-L283' title='Snippet source file'>snippet source</a> | <a href='#snippet-CountQueries' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Projections in Queries

### Anonymous Type Projections

```csharp
var productSummaries = session.Query<Product>()
    .Select(x => new 
{ 
        x.Id, 
        x.Name, 
        x.Price 
})
    .ToList();
```

### DTO Projections

```csharp
public class ProductSummary
{
public string Id { get; set; }
public string Name { get; set; }
public decimal Price { get; set; }
}

var summaries = session.Query<Product>()
    .Select(x => new ProductSummary 
{ 
        Id = x.Id, 
        Name = x.Name, 
        Price = x.Price 
})
    .ToList();
```

## Polymorphic Queries

### Query Base Type

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

// Query all animals
var allAnimals = session.Query<Animal>().ToList();
```

### Query Specific Type

```csharp
// Query only dogs
var dogs = session.Query<Dog>().ToList();

// Filter by type
var bigDogs = session.Query<Animal>()
    .OfType<Dog>()
    .Where(d => d.Breed == "Great Dane")
    .ToList();
```

## Raw SQL Queries

### Basic SQL Query

```csharp
using var session = store.OpenSession();

var sql = new SqlBuilder()
        .Append("SELECT * FROM Products")
        .Append("WHERE Price > @minPrice", new SqlParameter("minPrice", 100));
    
var products = session.Query<Product>(sql).ToList();
```

### SQL with Parameters

```csharp
var sql = new SqlBuilder()
    .Append("SELECT * FROM Products")
    .Append("WHERE CategoryId = @category", new SqlParameter("category", "electronics"))
    .Append("AND Price BETWEEN @minPrice AND @maxPrice",
        new SqlParameter("minPrice", 100),
        new SqlParameter("maxPrice", 1000));

var products = session.Query<Product>(sql).ToList();
```

### SQL with Joins

```csharp
var sql = new SqlBuilder()
    .Append("SELECT p.* FROM Products p")
    .Append("INNER JOIN Categories c ON p.CategoryId = c.Id")
    .Append("WHERE c.Name = @categoryName", new SqlParameter("categoryName", "Electronics"));

var products = session.Query<Product>(sql).ToList();
```

### Parameterized SQL

```csharp
var sql = new SqlBuilder(
parameters: new SqlParameter("status", "Active"),
new SqlParameter("minStock", 10))
    .Append("SELECT * FROM Products")
    .Append("WHERE Status = @status AND Stock >= @minStock");

var products = session.Query<Product>(sql).ToList();
```

## Column Projections

HybridDb uses projected columns for efficient querying:

```csharp
// Configuration
store.Configuration.Document<Product>()
    .With(x => x.Name)
    .With(x => x.Price)
    .With(x => x.CategoryId);

// Query uses projected columns
var products = session.Query<Product>()
    .Where(x => x.Price > 100 && x.CategoryId == "electronics")
    .ToList();

// Generated SQL uses columns:
// SELECT * FROM Products 
// WHERE Price > 100 AND CategoryId = 'electronics'
```

### Column Method

Query using column projections directly:

```csharp
var products = session.Query<Product>()
    .Where(x => x.Column<decimal>("Price") > 100)
    .ToList();
```

### In Operator

```csharp
var categories = new[] { "electronics", "computers", "phones" };

var products = session.Query<Product>()
    .Where(x => x.Column<string>("CategoryId").In(categories))
    .ToList();
```

## Query Performance

### Use Projected Columns

```csharp
// Fast: Uses projected column
var products = session.Query<Product>()
    .Where(x => x.CategoryId == "electronics")  // CategoryId is projected
    .ToList();

// Slow: Scans document JSON
var products = session.Query<Product>()
    .Where(x => x.SomeUnindexedProperty == "value")  // Not indexed
    .ToList();
```

### Query Statistics

```csharp
// Statistics are available through advanced API
var query = session.Query<Product>()
    .Where(x => x.Price > 100);

// Execute and get stats
var products = query.ToList();
```

## Query Patterns

### Search Pattern

```csharp
public List<Product> SearchProducts(string searchTerm, string category, decimal? minPrice, decimal? maxPrice)
{
     using var session = store.OpenSession();

var query = session.Query<Product>();
    
if (!string.IsNullOrEmpty(searchTerm))
{
        query = query.Where(x => x.Name.Contains(searchTerm));
}
    
if (!string.IsNullOrEmpty(category))
{
        query = query.Where(x => x.CategoryId == category);
}
    
if (minPrice.HasValue)
{
        query = query.Where(x => x.Price >= minPrice.Value);
}
    
if (maxPrice.HasValue)
{
        query = query.Where(x => x.Price <= maxPrice.Value);
}
    
return query.OrderBy(x => x.Name).ToList();
```

### Paginated Results

```csharp
public class PagedResult<T>
{
public List<T> Items { get; set; }
public int TotalCount { get; set; }
public int PageNumber { get; set; }
public int PageSize { get; set; }
public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public PagedResult<Product> GetProductsPage(int pageNumber, int pageSize)
{
    using (var session = store.OpenSession())
    {
        var query = session.Query<Product>()
            .Where(x => x.Status == "Active");
        
        var total = query.Count();
        
        var items = query
            .OrderBy(x => x.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        return new PagedResult<Product>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
```

### Existence Check

```csharp
public bool ProductExists(string productId)
{
    using (var session = store.OpenSession())
    {
        return session.Advanced.Exists<Product>(productId, out _);
    }
}

public bool HasProductsInCategory(string categoryId)
{
    using (var session = store.OpenSession())
    {
        return session.Query<Product>()
            .Any(x => x.CategoryId == categoryId);
    }
```

## Best Practices

### 1. Index Queried Properties

```csharp
// Configure projections for properties you query
store.Configuration.Document<Product>()
    .With(x => x.CategoryId)  // Queried frequently
    .With(x => x.Price)       // Queried frequently
    .With(x => x.Status);     // Queried frequently
```

### 2. Use Specific Queries

```csharp
// Good: Specific query
var product = session.Query<Product>()
    .Where(x => x.Id == productId)
    .FirstOrDefault();

// Better: Use Load for single document
var product = session.Load<Product>(productId);
```

### 3. Limit Result Sets

```csharp
// Good: Use Take to limit results
var recentProducts = session.Query<Product>()
    .OrderByDescending(x => x.CreatedDate)
    .Take(10)
    .ToList();

// Avoid: Loading all documents
var allProducts = session.Query<Product>().ToList();  // Could be huge!
```

### 4. Use Read-Only for Query-Only Operations

```csharp
public List<Product> GetProducts()
{
    using (var session = store.OpenSession())
    {
        // Documents won't be tracked or modified
        return session.Query<Product>()
            .Where(x => x.Status == "Active")
            .ToList();
    }
```

### 5. Combine Filters Efficiently

```csharp
// Good: Single query with multiple filters
var products = session.Query<Product>()
    .Where(x => x.CategoryId == "electronics" && x.Price > 100 && x.Stock > 0)
    .ToList();

// Avoid: Multiple database calls
var products = session.Query<Product>()
    .Where(x => x.CategoryId == "electronics")
    .ToList()
    .Where(x => x.Price > 100)  // In-memory filter
    .ToList();
```

## Troubleshooting

### Query Returns No Results

Check that:
1. Properties are indexed/projected
2. Values match exactly (case-sensitive for strings)
3. Documents exist in the table
4. No soft-delete filtering

### Slow Queries

Optimize by:
1. Adding database indexes on projected columns
2. Reducing result set with filters
3. Using specific queries instead of loading all
4. Checking query execution plan

### Property Not Queryable

```csharp
// Error: Property not indexed
var products = session.Query<Product>()
    .Where(x => x.UnindexedProperty == "value")  // Error!
    .ToList();

// Fix: Add projection
store.Configuration.Document<Product>()
    .With(x => x.UnindexedProperty);
```

### Type Conversion Issues

```csharp
// Use appropriate types in queries
var products = session.Query<Product>()
    .Where(x => x.Price > 100m)  // Use decimal for decimal properties
    .ToList();
```
