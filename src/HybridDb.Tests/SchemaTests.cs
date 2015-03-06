using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class SchemaTests : HybridDbTests
    {
        [Fact]
        public void ReturnsAllTables()
        {
            new CreateTable(new Table("Entities1")).Execute(store);
            new CreateTable(new Table("Entities2")).Execute(store);

            var schema = store.Schema.GetSchema();

            schema["Entities1"].ShouldNotBe(null);
            schema["Entities2"].ShouldNotBe(null);
        }

        [Fact]
        public void ReturnsAllColumns()
        {
            new CreateTable(new Table("Entities1")).Execute(store);
            new AddColumn("Entities1", new Column("SomeInt", typeof(int))).Execute(store);
            new AddColumn("Entities1", new Column("SomeBool", typeof(bool))).Execute(store);

            new CreateTable(new Table("Entities2")).Execute(store);
            new AddColumn("Entities2", new Column("SomeString", typeof(string))).Execute(store);

            var schema = store.Schema.GetSchema();

            schema["Entities1"]["SomeInt"].ShouldNotBe(null);
            schema["Entities1"]["SomeBool"].ShouldNotBe(null);
            schema["Entities2"]["SomeString"].ShouldNotBe(null);
        }

        [Fact]
        public void ColumnsHasTypeInfo()
        {
            Use(TableMode.UseTempTables);

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(store);
            new AddColumn("Entities1", new Column("SomeInt", typeof(int))).Execute(store);
            new AddColumn("Entities1", new Column("SomeBool", typeof(bool))).Execute(store);
            new AddColumn("Entities1", new Column("SomeString", typeof(string))).Execute(store);

            var schema = store.Schema.GetSchema();

            schema["Entities1"]["SomeInt"].Type.ShouldBe(typeof(int));
            schema["Entities1"]["SomeBool"].Type.ShouldBe(typeof(bool));
            schema["Entities1"]["SomeString"].Type.ShouldBe(typeof(string));
        }
    }
}