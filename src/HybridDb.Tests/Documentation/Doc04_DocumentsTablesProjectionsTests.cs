using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Schema;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using SqlCommand = HybridDb.Migrations.Schema.Commands.SqlCommand;

namespace HybridDb.Tests.Documentation;

public class Doc04_DocumentsTablesProjectionsTests(ITestOutputHelper output) : DocumentationTestBase(output)
{
    [Fact(Skip = "Code example - not meant for execution")]
    public void BasicDocumentRegistration()
    {
        #region BasicDocumentRegistration
        var store = DocumentStore.Create(config =>
        {
            config.Document<Product>();
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void CustomTableNames()
    {
        #region CustomTableNames
        var store = DocumentStore.Create(config =>
        {
            config.Document<Product>("MyProductTable");
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void CustomDiscriminators()
    {
        #region CustomDiscriminators
        var store = DocumentStore.Create(config =>
        {
            config.Document<Product>(discriminator: "Prod");
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void SimpleProjections()
    {
        #region SimpleProjections
        var store = DocumentStore.Create(config =>
        {
            config.Document<Product>()
                .With(x => x.Name)
                .With(x => x.Price)
                .With(x => x.CategoryId);
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void CustomColumnNames()
    {
        #region CustomColumnNames
        var store = DocumentStore.Create(config =>
        {
            config.Document<Product>()
                .With("ProductName", x => x.Name)
                .With("ProductPrice", x => x.Price);
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void NestedProperties()
    {
        #region NestedProperties
        var store = DocumentStore.Create(config =>
        {
            config.Document<Order>()
                .With("ShippingCity", x => x.ShippingAddress.City)
                .With("ShippingCountry", x => x.ShippingAddress.Country)
                .With(x => x.Total);
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void TransformingValues()
    {
        #region TransformingValues
        var store = DocumentStore.Create(config =>
        {
            config.Document<ExtendedProduct>()
                .With(x => x.Price, price => Math.Round(price, 2))
                .With(x => x.Name, name => name.ToUpperInvariant())
                .With("ShippingCity", x => x.ShippingAddress.City, city => city.ToLowerInvariant());
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void CalculatedProjections()
    {
        #region CalculatedProjections
        var store = DocumentStore.Create(config =>
        {
            config.Document<ExtendedProduct>()
                .With("FullName", x => $"{x.Brand} {x.Name}")
                .With("IsExpensive", x => x.Price > 1000)
                .With("PriceWithTax", x => x.Price * 1.25m);
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void JsonProjections()
    {
        #region JsonProjections
        var store = DocumentStore.Create(config =>
        {
            config.Document<ExtendedProduct>()
                .With(x => x.Tags, x => x, new AsJson());

            // Or with custom column name
            config.Document<ExtendedProduct>()
                .With("ProductTags", x => x.Tags, x => x, new AsJson());
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void MaxLengthOption()
    {
        #region MaxLengthOption
        var store = DocumentStore.Create(config =>
        {
            config.Document<ExtendedProduct>()
                .With(x => x.Description, new MaxLength(1000));
        });

        // Default for strings is 850 if not specified
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void AsJsonOption()
    {
        #region AsJsonOption
        var store = DocumentStore.Create(config =>
        {
            config.Document<ExtendedProduct>()
                .With(x => x.Specifications, new AsJson());
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void DisableNullCheckInjectionOption()
    {
        #region DisableNullCheckInjectionOption
        var store = DocumentStore.Create(config =>
        {
            config.Document<ExtendedProduct>()
                .With(x => x.Name, new DisableNullCheckInjection());
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void CustomKeyMethod()
    {
        #region CustomKeyMethod
        var store = DocumentStore.Create(config =>
        {
            config.Document<ExtendedProduct>()
                .Key(x => x.ProductCode);
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void GlobalKeyResolver()
    {
        #region GlobalKeyResolver
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
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void BasicPolymorphism()
    {
        #region BasicPolymorphism
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
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void QueryingPolymorphicTypes()
    {
        using var session = store.OpenSession();

        session.Store(new Dog { Id = "dog-1", Name = "Buddy", Breed = "Great Dane" });
        session.Store(new Cat { Id = "cat-1", Name = "Whiskers", Lives = 9 });
        session.SaveChanges();

        #region QueryingPolymorphicTypes
        // Query all animals
        var allAnimals = session.Query<Animal>().ToList();

        // Query only dogs
        var dogs = session.Query<Dog>().ToList();

        // Query with type filtering
        var bigDogs = session.Query<Animal>()
            .OfType<Dog>()
            .Where(d => d.Breed == "Great Dane")
            .ToList();
        #endregion

        allAnimals.Count.ShouldBe(2);
        dogs.Count.ShouldBe(1);
        bigDogs.Count.ShouldBe(1);
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void SeparateTablesForDerivedTypes()
    {
        #region SeparateTablesForDerivedTypes
        var store = DocumentStore.Create(config =>
        {
            config.Document<Animal>("Animals");
            config.Document<Dog>("Dogs");  // Separate table
            config.Document<Cat>("Cats");  // Separate table
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void AccessingTableConfiguration()
    {
        #region AccessingTableConfiguration
        var design = store.Configuration.GetDesignFor<Product>();
        var table = design.Table;

        Console.WriteLine($"Table: {table.Name}");
        foreach (var column in table.Columns)
        {
            Console.WriteLine($"  {column.Name}: {column.Type}");
        }
        #endregion

        table.ShouldNotBeNull();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void ProjectionsAndIndexes()
    {
        #region ProjectionsAndIndexes
        var migration = new MyMigration();
        
        var store = DocumentStore.Create(config =>
        {
            config.Document<Product>();
            config.UseMigrations(new List<Migration> { migration });
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void CompositeIndexes()
    {
        #region CompositeIndexes
        var migration = new CompositeIndexMigration();
        
        var store = DocumentStore.Create(config =>
        {
            config.Document<Product>();
            config.UseMigrations(new List<Migration> { migration });
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void ExtendedProjections()
    {
        #region ExtendedProjections
        var store = DocumentStore.Create(config =>
        {
            config.Document<Product>()
                .Extend<ProductIndex>(index =>
                {
                    index.With(x => x.Category, p => p.Category);
                    index.With(x => x.Supplier, p => p.Category);
                });
        });
        #endregion

        store.Dispose();
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void ProjectionWithMetadata()
    {
        #region ProjectionWithMetadata
        var projection = Projection.From<string>((document, metadata) =>
        {
            var product = document as Product;
            var createdBy = metadata.TryGetValue("CreatedBy", out var value) 
                ? value.FirstOrDefault() 
                : "Unknown";
            
            return $"{product?.Name} (by {createdBy})";
        });
        #endregion

        projection.ShouldNotBeNull();
    }

    #region MyMigration
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
    #endregion

    #region CompositeIndexMigration
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
    #endregion

    #region SupportingTypes
    public class ExtendedProduct : Product
    {
        public string Brand { get; set; }
        public string Description { get; set; }
        public string ProductCode { get; set; }
        public Address ShippingAddress { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, string> Specifications { get; set; }
    }

    public new class Order
    {
        public string Id { get; set; }
        public Address ShippingAddress { get; set; }
        public decimal Total { get; set; }
        public string OrderNumber { get; set; }
    }

    public new class Address
    {
        public string City { get; set; }
        public string Country { get; set; }
    }

    #region AnimalHierarchy
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
    #endregion

    public class ProductIndex
    {
        public string Category { get; set; }
        public string Supplier { get; set; }
    }
    #endregion
}
