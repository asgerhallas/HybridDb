using System;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RemoveColumnTests : HybridDbTests
    {
        public RemoveColumnTests(ITestOutputHelper output) : base(output) => NoInitialize();

        [Fact]
        public void RemovesColumn()
        {
            Use(TableMode.RealTables);

            var table = new Table("Entities", new Column("FirstColumn", typeof (int)));
            store.Execute(new CreateTable(table));
            store.Execute(new AddColumn("Entities", new Column("SomeColumn", typeof(int))));

            store.Execute(new RemoveColumn(table, "SomeColumn"));

            //store.Database.QuerySchema()["Entities"]["SomeColumn"].ShouldBe(null);
        }

        [Theory(Skip = "Bug in SQL server 2012 prevents us from removing temp tables")]
        [InlineData(TableMode.GlobalTempTables)]
        public void RemovesTempTableColumn(TableMode mode)
        {
            Use(mode);

            var table = new Table("Entities", new Column("FirstColumn", typeof(int)));
            store.Execute(new CreateTable(table));
            store.Execute(new AddColumn("Entities", new Column("SomeColumn", typeof(int))));

            store.Execute(new RemoveColumn(table, "SomeColumn"));

            //store.Database.QuerySchema()["Entities"]["SomeColumn"].ShouldBe(null);
        }

        [Theory]
        [InlineData("Document", true)]
        [InlineData("Id", true)]
        [InlineData("OtherName", true)]
        public void IsAlwaysUnsafe(string columnName, bool isUnsafe)
        {
            new RemoveColumn(new Table("Entities"), columnName).Safe.ShouldBe(!isUnsafe);
        }

        [Theory]
        [InlineData("Document")]
        [InlineData("Id")]
        public void ThrowsOnBuiltInColumns(string columnName)
        {
            Should.Throw<InvalidOperationException>(() => new RemoveColumn(new DocumentTable("test"), columnName))
                .Message.ShouldBe($"You can not remove build in column {columnName}.");
        }

        [Theory]
        [InlineData("DocumentX")]
        [InlineData("IdY")]
        public void NoThrowOnNotBuiltInColumns(string columnName)
        {
            var documentTable = new DocumentTable("test");
            documentTable.Add(new Column<string>("DocumentX", defaultValue: "asger"));
            documentTable.Add(new Column<Guid>("IdY", defaultValue: Guid.Empty));

            Should.NotThrow(() => new RemoveColumn(documentTable, columnName));
        }

        [Fact]
        public void DoesNotRequireReProjection()
        {
            new RemoveColumn(new Table("Entities"), "Col").RequiresReprojectionOf.ShouldBe(null);
        }
    }
}