using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RemoveColumnTests : HybridDbTests
    {
        [Fact]
        public void RemovesColumn()
        {
            var table = new Table("Entities", new Column("FirstColumn", typeof (int)));
            Execute(new CreateTable(table));
            Execute(new AddColumn("Entities", new Column("SomeColumn", typeof(int))));

            Execute(new RemoveColumn(table, "SomeColumn"));

            //store.Database.QuerySchema()["Entities"]["SomeColumn"].ShouldBe(null);
        }

        [Fact]
        public void RemovesTempTableColumn()
        {
            var table = new Table("Entities", new Column("FirstColumn", typeof(int)));
            Execute(new CreateTable(table));
            Execute(new AddColumn("Entities", new Column("SomeColumn", typeof(int))));

            Execute(new RemoveColumn(table, "SomeColumn"));

            //store.Database.QuerySchema()["Entities"]["SomeColumn"].ShouldBe(null);
        }

        [Theory]
        [InlineData("Document", true)]
        [InlineData("Id", true)]
        [InlineData("OtherName", true)]
        public void IsAlwaysUnsafe(string columnName, bool isUnsafe)
        {
            new RemoveColumn(new Table("Entities"), columnName).Unsafe.ShouldBe(isUnsafe);
        }

        [Fact]
        public void DoesNotRequireReProjection()
        {
            new RemoveColumn(new Table("Entities"), "Col").RequiresReprojectionOf.ShouldBe(null);
        }
    }
}