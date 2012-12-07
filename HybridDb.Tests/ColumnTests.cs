using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Xunit;
using Shouldly;

namespace HybridDb.Tests
{
    public class ColumnTests
    {
        [Fact]
        public void GetValueFromDocumentColumn()
        {
            var serializer = new JsonSerializer();
            var column = new DocumentColumn(typeof(object), serializer);
            var bytes = (byte[])column.GetValue(new {hat = "briller"});
            using(var inStream = new MemoryStream(bytes))
            using(var bsonReader = new BsonReader(inStream))
            {
                var document = (dynamic) serializer.Deserialize(bsonReader);
                Assert.Equal("briller", (string)document.hat);
            }
        }

        [Fact]
        public void GetValueFromDefaultColumn()
        {
            var column = new ProjectionColumn<Entity, string>(x => x.Bryststørrelse);
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