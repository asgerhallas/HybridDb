using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RemoveColumnTests : HybridDbTests
    {
        public RemoveColumnTests() => NoInitialize();

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

        [Fact]
        public void DoesNotRequireReProjection()
        {
            new RemoveColumn(new Table("Entities"), "Col").RequiresReprojectionOf.ShouldBe(null);
        }
    }
}