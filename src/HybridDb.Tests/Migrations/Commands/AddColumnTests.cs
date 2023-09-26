using System;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class AddColumnTests : HybridDbTests
    {
        public AddColumnTests(ITestOutputHelper output) : base(output) => NoInitialize();

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void AddsColumn(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());

            store.Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))));

            store.Execute(new AddColumn("Entities", new Column("Col2", typeof(int))));

            store.Database.QuerySchema()["Entities"].ShouldContain("Col2");
        }

        [Theory]
        [InlineData(TableMode.GlobalTempTables, typeof(int), false)]
        [InlineData(TableMode.RealTables, typeof(int), false)]
        [InlineData(TableMode.GlobalTempTables, typeof(double), false)]
        [InlineData(TableMode.RealTables, typeof(double), false)]
        [InlineData(TableMode.GlobalTempTables, typeof(string), true)]
        [InlineData(TableMode.RealTables, typeof(string), true)]
        [InlineData(TableMode.GlobalTempTables, typeof(decimal), false)]
        [InlineData(TableMode.RealTables, typeof(decimal), false)]
        public void ColumnIsOfCorrectType(TableMode mode, Type type, bool nullable)
        {
            Use(TableMode.RealTables);
            store.Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))));

            store.Execute(new AddColumn("Entities", new Column("Col2", type)));

            //store.Database.QuerySchema()["Entities"]["Col2"].Type.ShouldBe(type);
            //store.Database.QuerySchema()["Entities"]["Col2"].Nullable.ShouldBe(nullable);
        }

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void SetsColumnAsNullableAndUsesUnderlyingTypeWhenNullable(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            store.Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))));

            store.Execute(new AddColumn("Entities", new Column("Col2", typeof(int?))));

            //store.Database.QuerySchema()["Entities"]["Col2"].Type.ShouldBe(typeof(int));
            //store.Database.QuerySchema()["Entities"]["Col2"].Nullable.ShouldBe(true);
        }

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void CanSetColumnAsPrimaryKey(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());

            store.Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));
            store.Execute(new AddColumn("Entities1", new Column("SomeInt", typeof(int), isPrimaryKey: true)));

            //store.Database.QuerySchema()["Entities1"]["SomeInt"].IsPrimaryKey.ShouldBe(true);
        }

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void CanAddColumnWithDefaultValue(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            store.Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));

            store.Execute(new AddColumn("Entities1", new Column("SomeNullableInt", typeof(int?), defaultValue: null)));
            store.Execute(new AddColumn("Entities1", new Column("SomeOtherNullableInt", typeof(int?), defaultValue: 42)));
            store.Execute(new AddColumn("Entities1", new Column("SomeString", typeof(string), defaultValue: "peter")));
            store.Execute(new AddColumn("Entities1", new Column("SomeInt", typeof(int),  defaultValue: 666)));
            store.Execute(new AddColumn("Entities1", new Column("SomeDateTime", typeof(DateTime),  defaultValue: new DateTime(1999, 12, 24))));

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
            store.Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));
            store.Execute(new AddColumn("Entities1", new Column("SomeString", typeof(string), defaultValue: "'; DROP TABLE #Entities1; SELECT '")));

            store.Database.QuerySchema().ShouldContainKey("Entities1");
        }

        [Fact]
        public void IsSafe()
        {
            new AddColumn("Entities", new Column("Col", typeof(int))).Safe.ShouldBe(true);
        }

        [Fact]
        public void RequiresReprojection()
        {
            new AddColumn("Entities", new Column("Col", typeof(int))).RequiresReprojectionOf.ShouldBe("Entities");
        }
    }
}