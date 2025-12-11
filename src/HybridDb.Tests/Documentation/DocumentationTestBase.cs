using System;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace HybridDb.Tests.Documentation
{
    /// <summary>
    /// Base class for documentation tests with common entities and helpers
    /// </summary>
    public abstract class DocumentationTestBase : HybridDbTests
    {
        protected DocumentationTestBase(ITestOutputHelper output) : base(output)
        {
            UseGlobalTempTables();
        }

        // Common entities used across documentation
        public class Product
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public string CategoryId { get; set; }
            public int Stock { get; set; }
            public string Category { get; set; }
        }

        public class Order
        {
            public string Id { get; set; }
            public string ProductId { get; set; }
            public string CustomerId { get; set; }
            public DateTime OrderDate { get; set; }
            public int Quantity { get; set; }
        }

        public class Customer
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public Address Address { get; set; }
        }

        public class Address
        {
            public string Street { get; set; }
            public string City { get; set; }
            public string Country { get; set; }
        }

        public class MyEntity
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Property { get; set; }
        }

        public class ProcessedOrder
        {
            public string Id { get; set; }
        }

        // Helper methods
        protected void ProcessPayment() { }
        protected void NotifyWarehouse() { }
    }
}
