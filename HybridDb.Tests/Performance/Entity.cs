using System;

namespace HybridDb.Tests.Performance
{
    public class Entity
    {
        public Guid Id { get; set; }
        public string SomeData { get; set; }
        public int SomeNumber { get; set; }
    }
}