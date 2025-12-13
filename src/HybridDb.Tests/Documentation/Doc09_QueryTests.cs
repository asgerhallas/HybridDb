using System;
using System.Linq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Documentation
{
    /// <summary>
    /// Tests for code samples in: docs/09-documentsession-query.md
    /// </summary>
    public class Doc09_QueryTests : DocumentationTestBase
    {
        public Doc09_QueryTests(ITestOutputHelper output) : base(output)
        {
        }

        private void SetupTestData()
        {
            Document<Product>()
                .With(x => x.Name)
                .With(x => x.Price)
                .With(x => x.CategoryId)
                .With(x => x.Stock);

            store.Transactionally(tx =>
            {
                using var session = store.OpenSession(tx);
                session.Store(new Product { Id = "p1", Name = "Widget", Price = 150m, CategoryId = "electronics", Stock = 10 });
                session.Store(new Product { Id = "p2", Name = "Gadget", Price = 50m, CategoryId = "electronics", Stock = 5 });
                session.Store(new Product { Id = "p3", Name = "Gizmo", Price = 1200m, CategoryId = "computers", Stock = 2 });
                session.Store(new Product { Id = "p4", Name = "Pro Widget 2000", Price = 200m, CategoryId = "electronics", Stock = 0 });
                session.SaveChanges();
            });
        }

        [Fact]
        public void BasicLINQQuery()
        {
            SetupTestData();

            #region BasicLINQQuery
            using var session = store.OpenSession();

            var products = session.Query<Product>()
                .Where(x => x.Price > 100)
                .ToList();
            #endregion

            products.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void QueryAllDocuments()
        {
            SetupTestData();

            #region QueryAllDocuments
            using var session = store.OpenSession();

            var allProducts = session.Query<Product>().ToList();
            #endregion

            allProducts.Count.ShouldBe(4);
        }

        [Fact]
        public void QueryWithSingleResult()
        {
            SetupTestData();

            #region QueryWithSingleResult
            using var session = store.OpenSession();

            var product = session.Query<Product>()
                .Where(x => x.Name == "Widget")
                .SingleOrDefault();
            #endregion

            product.ShouldNotBeNull();
        }

        [Fact]
        public void QueryWithFirst()
        {
            SetupTestData();

            #region QueryWithFirst
            using var session = store.OpenSession();

            var product = session.Query<Product>()
                .Where(x => x.Price > 50)
                .FirstOrDefault();
            #endregion

            product.ShouldNotBeNull();
        }

        [Fact]
        public void WhereEquality()
        {
            SetupTestData();

            #region WhereEquality
            var session = store.OpenSession();
            
            var products = session.Query<Product>()
                .Where(x => x.CategoryId == "electronics")
                .ToList();
            #endregion

            products.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void ComparisonOperators()
        {
            SetupTestData();

            using var session = store.OpenSession();

            #region ComparisonOperators
            // Greater than
            var expensive = session.Query<Product>()
                .Where(x => x.Price > 1000)
                .ToList();

            // Less than or equal
            var affordable = session.Query<Product>()
                .Where(x => x.Price <= 50)
                .ToList();
            #endregion

            expensive.Count.ShouldBeGreaterThan(0);
            affordable.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void LogicalOperators()
        {
            SetupTestData();

            using var session = store.OpenSession();

            #region LogicalOperators
            // AND
            var filtered = session.Query<Product>()
                .Where(x => x.Price > 100 && x.CategoryId == "electronics")
                .ToList();

            // OR
            var multiple = session.Query<Product>()
                .Where(x => x.CategoryId == "electronics" || x.CategoryId == "computers")
                .ToList();
            #endregion

            filtered.Count.ShouldBeGreaterThan(0);
            multiple.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void MultipleWhereClauses()
        {
            SetupTestData();

            using var session = store.OpenSession();

            #region MultipleWhereClauses
            var products = session.Query<Product>()
                .Where(x => x.Price > 50)
                .Where(x => x.CategoryId == "electronics")
                .Where(x => x.Stock > 0)
                .ToList();
            #endregion

            products.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void StringOperations()
        {
            SetupTestData();

            using var session = store.OpenSession();

            #region StringOperations
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
            #endregion

            containsWidget.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void OrderingResults()
        {
            SetupTestData();

            using var session = store.OpenSession();

            #region OrderingResults
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
            #endregion

            byPrice.Count.ShouldBeGreaterThan(0);
            byPrice.First().Price.ShouldBeLessThanOrEqualTo(byPrice.Last().Price);
        }

        [Fact]
        public void TakeAndSkip()
        {
            SetupTestData();

            using var session = store.OpenSession();

            #region TakeAndSkip
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
            #endregion

            firstPage.Count.ShouldBeLessThanOrEqualTo(10);
        }

        [Fact]
        public void CountQueries()
        {
            SetupTestData();

            using var session = store.OpenSession();

            #region CountQueries
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
            #endregion

            totalCount.ShouldBe(4);
            hasProducts.ShouldBeTrue();
        }
    }
}
