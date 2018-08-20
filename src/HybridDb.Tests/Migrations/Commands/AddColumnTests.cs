using System;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class AddColumnTests : HybridDbTests
    {
        [Fact]
        public void AddsColumn()
        {
            Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))));

            Execute(new AddColumn("Entities", new Column("Col2", typeof(int))));

            store.Database.QuerySchema()["Entities"].ShouldContain("Col2");
        }

        [Theory]
        [InlineData(typeof(int), false)]
        [InlineData(typeof(double), false)]
        [InlineData(typeof(string), true)]
        [InlineData(typeof(decimal), false)]
        public void ColumnIsOfCorrectType(Type type, bool nullable)
        {
            Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))));

            Execute(new AddColumn("Entities", new Column("Col2", type)));

            //store.Database.QuerySchema()["Entities"]["Col2"].Type.ShouldBe(type);
            //store.Database.QuerySchema()["Entities"]["Col2"].Nullable.ShouldBe(nullable);
        }

        [Fact]
        public void SetsColumnAsNullableAndUsesUnderlyingTypeWhenNullable()
        {
            Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))));

            Execute(new AddColumn("Entities", new Column("Col2", typeof(int?))));

            //store.Database.QuerySchema()["Entities"]["Col2"].Type.ShouldBe(typeof(int));
            //store.Database.QuerySchema()["Entities"]["Col2"].Nullable.ShouldBe(true);
        }

        [Fact]
        public void CanSetColumnAsPrimaryKey()
        {
            Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));
            Execute(new AddColumn("Entities1", new Column("SomeInt", typeof(int), isPrimaryKey: true)));

            //store.Database.QuerySchema()["Entities1"]["SomeInt"].IsPrimaryKey.ShouldBe(true);
        }

        [Fact]
        public void CanAddColumnWithDefaultValue()
        {
            Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));

            Execute(new AddColumn("Entities1", new Column("SomeNullableInt", typeof(int?), defaultValue: null)));
            Execute(new AddColumn("Entities1", new Column("SomeOtherNullableInt", typeof(int?), defaultValue: 42)));
            Execute(new AddColumn("Entities1", new Column("SomeString", typeof(string), defaultValue: "peter")));
            Execute(new AddColumn("Entities1", new Column("SomeInt", typeof(int),  defaultValue: 666)));
            Execute(new AddColumn("Entities1", new Column("SomeDateTime", typeof(DateTime),  defaultValue: new DateTime(1999, 12, 24))));

            var schema = store.Database.QuerySchema();

            //schema["Entities1"]["SomeNullableInt"].DefaultValue.ShouldBe(null);
            //schema["Entities1"]["SomeOtherNullableInt"].DefaultValue.ShouldBe(42);
            //schema["Entities1"]["SomeString"].DefaultValue.ShouldBe("peter");
            //schema["Entities1"]["SomeInt"].DefaultValue.ShouldBe(666);
            //schema["Entities1"]["SomeDateTime"].DefaultValue.ShouldBe(new DateTime(1999, 12, 24));
        }

        [Fact(Skip = "Not solved yet")]
        public void ShouldNotAllowSqlInjection()
        {
            Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));
            Execute(new AddColumn("Entities1", new Column("SomeString", typeof(string), defaultValue: "'; DROP TABLE #Entities1; SELECT '")));

            store.Database.QuerySchema().ShouldContainKey("Entities1");
        }

        [Fact]
        public void IsSafe()
        {
            new AddColumn("Entities", new Column("Col", typeof(int))).Unsafe.ShouldBe(false);
        }

        [Fact]
        public void RequiresReprojection()
        {
            new AddColumn("Entities", new Column("Col", typeof(int))).RequiresReprojectionOf.ShouldBe("Entities");
        }
    }
}