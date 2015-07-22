using System;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit.Extensions;

namespace HybridDb.Tests
{
    public class DatabaseQuerySchemaTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void ReturnsAllTables(TableMode mode)
        {
            Use(mode, prefix: Guid.NewGuid().ToString());

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(database);
            new CreateTable(new Table("Entities2", new Column("test", typeof(int)))).Execute(database);

            var schema = database.QuerySchema();

            schema["Entities1"].ShouldNotBe(null);
            schema["Entities1"].Name.ShouldBe("Entities1");
            schema["Entities2"].ShouldNotBe(null);
            schema["Entities2"].Name.ShouldBe("Entities2");
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void ReturnsAllColumns(TableMode mode)
        {
            Use(mode, prefix: Guid.NewGuid().ToString());

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(database);
            new AddColumn("Entities1", new Column("SomeInt", typeof(int))).Execute(database);
            new AddColumn("Entities1", new Column("SomeBool", typeof(bool))).Execute(database);

            new CreateTable(new Table("Entities2", new Column("test", typeof(int)))).Execute(database);
            new AddColumn("Entities2", new Column("SomeString", typeof(string))).Execute(database);

            var schema = database.QuerySchema();

            schema["Entities1"]["SomeInt"].ShouldNotBe(null);
            schema["Entities1"]["SomeBool"].ShouldNotBe(null);
            schema["Entities2"]["SomeString"].ShouldNotBe(null);
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void ColumnsHasTypeInfo(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(database);
            new AddColumn("Entities1", new Column("SomeInt", typeof(int))).Execute(database);
            new AddColumn("Entities1", new Column("SomeBool", typeof(bool))).Execute(database);
            new AddColumn("Entities1", new Column("SomeString", typeof(string))).Execute(database);

            var schema = database.QuerySchema();

            schema["Entities1"]["SomeInt"].Type.ShouldBe(typeof(int));
            schema["Entities1"]["SomeBool"].Type.ShouldBe(typeof(bool));
            schema["Entities1"]["SomeString"].Type.ShouldBe(typeof(string));
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void ColumnsHasNullableInfo(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(database);
            new AddColumn("Entities1", new Column("SomeNullableInt", typeof(int?))).Execute(database);
            new AddColumn("Entities1", new Column("SomeString", typeof(string))).Execute(database);
            new AddColumn("Entities1", new Column("SomeInt", typeof(int))).Execute(database);

            var schema = database.QuerySchema();

            schema["Entities1"]["SomeNullableInt"].Nullable.ShouldBe(true);
            schema["Entities1"]["SomeInt"].Nullable.ShouldBe(false);
            schema["Entities1"]["SomeString"].Nullable.ShouldBe(true);
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void ColumnsHasPrimaryKeyInfo(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(database);
            new AddColumn("Entities1", new Column("SomeInt", typeof(int), isPrimaryKey: true)).Execute(database);
            new AddColumn("Entities1", new Column("SomeString", typeof(string),  isPrimaryKey: false)).Execute(database);

            var schema = database.QuerySchema();

            schema["Entities1"]["SomeInt"].IsPrimaryKey.ShouldBe(true);
            schema["Entities1"]["SomeString"].IsPrimaryKey.ShouldBe(false);
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void ColumnsHasDefaultValue(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(database);
            
            new AddColumn("Entities1", new Column("SomeNullableInt", typeof(int?), defaultValue: null)).Execute(database);
            new AddColumn("Entities1", new Column("SomeOtherNullableInt", typeof(int?), defaultValue: 42)).Execute(database);
            new AddColumn("Entities1", new Column("SomeNullableDecimal", typeof(decimal?), defaultValue: null)).Execute(database);
            new AddColumn("Entities1", new Column("SomeOtherNullableDecimal", typeof(decimal?), defaultValue: 42.42m)).Execute(database);
            new AddColumn("Entities1", new Column("SomeNullableGuid", typeof(Guid?), defaultValue: null)).Execute(database);
            new AddColumn("Entities1", new Column("SomeOtherNullableGuid", typeof(Guid?), defaultValue: new Guid("BAB0E469-DE94-4FDB-9868-968CA569E9A9"))).Execute(database);
            new AddColumn("Entities1", new Column("SomeNullableDateTime", typeof(DateTime?), defaultValue: null)).Execute(database);
            new AddColumn("Entities1", new Column("SomeOtherNullableDateTime", typeof(DateTime?), defaultValue: new DateTime(1999, 12, 24))).Execute(database);
            new AddColumn("Entities1", new Column("SomeNullableDateTimeOffset", typeof(DateTimeOffset?), defaultValue: null)).Execute(database);
            new AddColumn("Entities1", new Column("SomeOtherNullableDateTimeOffset", typeof(DateTimeOffset?), defaultValue: new DateTimeOffset(new DateTime(1999, 12, 24)))).Execute(database);

            new AddColumn("Entities1", new Column("SomeGuid", typeof(Guid), defaultValue: new Guid("BAB0E469-DE94-4FDB-9868-968CA569E9A9"))).Execute(database);
            new AddColumn("Entities1", new Column("SomeInt", typeof(int), defaultValue: 666)).Execute(database);
            new AddColumn("Entities1", new Column("SomeDecimal", typeof(decimal), defaultValue: 666.22m)).Execute(database);
            new AddColumn("Entities1", new Column("SomeDouble", typeof(double), defaultValue: 666.42)).Execute(database);
            new AddColumn("Entities1", new Column("SomeLong", typeof(long), defaultValue: 987654321987654312)).Execute(database);
            new AddColumn("Entities1", new Column("SomeString", typeof(string), defaultValue: "peter")).Execute(database);
            new AddColumn("Entities1", new Column("SomeOtherString", typeof(string), defaultValue: null)).Execute(database);
            new AddColumn("Entities1", new Column("SomeBool", typeof(bool), defaultValue: true)).Execute(database);
            new AddColumn("Entities1", new Column("SomeOtherBool", typeof(bool))).Execute(database);
            new AddColumn("Entities1", new Column("SomeDateTime", typeof(DateTime),  defaultValue: new DateTime(1999, 12, 24))).Execute(database);
            new AddColumn("Entities1", new Column("SomeDateTimeOffset", typeof(DateTimeOffset), defaultValue: new DateTimeOffset(new DateTime(1999, 12, 24)))).Execute(database);
            new AddColumn("Entities1", new Column("SomeEnum", typeof(SomeEnum), defaultValue: SomeEnum.SomeOtherValue)).Execute(database);

            var schema = database.QuerySchema();

            schema["Entities1"]["SomeNullableInt"].DefaultValue.ShouldBe(null);
            schema["Entities1"]["SomeOtherNullableInt"].DefaultValue.ShouldBe(42);
            schema["Entities1"]["SomeNullableDecimal"].DefaultValue.ShouldBe(null);
            schema["Entities1"]["SomeOtherNullableDecimal"].DefaultValue.ShouldBe(42.42);
            schema["Entities1"]["SomeNullableGuid"].DefaultValue.ShouldBe(null);
            schema["Entities1"]["SomeOtherNullableGuid"].DefaultValue.ShouldBe(new Guid("BAB0E469-DE94-4FDB-9868-968CA569E9A9"));
            schema["Entities1"]["SomeNullableDateTime"].DefaultValue.ShouldBe(null);
            schema["Entities1"]["SomeOtherNullableDateTime"].DefaultValue.ShouldBe(new DateTime(1999, 12, 24));
            schema["Entities1"]["SomeNullableDateTimeOffset"].DefaultValue.ShouldBe(null);
            schema["Entities1"]["SomeOtherNullableDateTimeOffset"].DefaultValue.ShouldBe(new DateTimeOffset(new DateTime(1999, 12, 24)));

            schema["Entities1"]["SomeGuid"].DefaultValue.ShouldBe(new Guid("BAB0E469-DE94-4FDB-9868-968CA569E9A9"));
            schema["Entities1"]["SomeInt"].DefaultValue.ShouldBe(666);
            schema["Entities1"]["SomeDecimal"].DefaultValue.ShouldBe(666.22);
            schema["Entities1"]["SomeDouble"].DefaultValue.ShouldBe(666.42);
            schema["Entities1"]["SomeLong"].DefaultValue.ShouldBe(987654321987654312);
            schema["Entities1"]["SomeString"].DefaultValue.ShouldBe("peter");
            schema["Entities1"]["SomeOtherString"].DefaultValue.ShouldBe(null);
            schema["Entities1"]["SomeBool"].DefaultValue.ShouldBe(true);
            schema["Entities1"]["SomeOtherBool"].DefaultValue.ShouldBe(false);
            schema["Entities1"]["SomeDateTime"].DefaultValue.ShouldBe(new DateTime(1999, 12, 24));
            schema["Entities1"]["SomeDateTimeOffset"].DefaultValue.ShouldBe(new DateTimeOffset(new DateTime(1999, 12, 24)));
            schema["Entities1"]["SomeEnum"].DefaultValue.ShouldBe("SomeOtherValue");
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void CanHandleLegacyBooleanDefaultValues(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(database);

            database.RawExecute(string.Format("alter table {0} add [SomeFalseBool] Bit NOT NULL DEFAULT '0'", database.FormatTableNameAndEscape("Entities1")));
            database.RawExecute(string.Format("alter table {0} add [SomeTrueBool] Bit NOT NULL DEFAULT '1'", database.FormatTableNameAndEscape("Entities1")));

            var schema = database.QuerySchema();

            schema["Entities1"]["SomeFalseBool"].DefaultValue.ShouldBe(false);
            schema["Entities1"]["SomeTrueBool"].DefaultValue.ShouldBe(true);
        }

        enum SomeEnum
        {
            SomeValue,
            SomeOtherValue,
        }
    }
}