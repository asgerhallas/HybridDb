using System;
using HybridDb.Schema;
using Xunit;
using Shouldly;

namespace HybridDb.Tests
{
    public class ColumnTests
    {
        [Fact]
        public void GetValueFromDefaultColumn()
        {
            var column = new ProjectionColumn<Entity, string>("Bryststørrelse", x => x.Bryststørrelse);
            var value = column.GetValue(new Entity {Bryststørrelse = "DD"});
            value.ShouldBe("DD");
        }

        [Fact]
        public void GetValueFromIdColumn()
        {
            var column = new IdColumn();
            var document = new Entity {Id = Guid.NewGuid()};
            var value = column.GetValue(document);
            value.ShouldBe(document.Id);
        }

        public class Entity
        {
            public Guid Id { get; set; }
            public string Bryststørrelse { get; set; }
        }
    }
}