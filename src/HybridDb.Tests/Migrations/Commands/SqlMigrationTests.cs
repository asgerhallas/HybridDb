using System;
using System.Data;
using System.Data.SqlClient;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit;
using Xunit.Abstractions;
using SqlCommand = HybridDb.Migrations.Schema.Commands.SqlCommand;

namespace HybridDb.Tests.Migrations.Commands
{
    public class SqlMigrationTests : HybridDbTests
    {
        public SqlMigrationTests(ITestOutputHelper output) : base(output) => NoInitialize();

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void AddsColumn(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            store.Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))));
            store.Execute(new AddColumn("Entities", new Column("Col2", typeof(int))));

            store.Execute(new SqlCommand("add some index", (sql, db) => sql
                .Append($"alter table {store.Database.FormatTableNameAndEscape("Entities")} add {store.Database.Escape("Col3")} int")));
        }

        [Fact]
        public void CanUseParameters()
        {
            store.Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))));
            store.Execute(new AddColumn("Entities", new Column("Col2", typeof(int))));

            store.Execute(new SqlCommand("add some data", (sql, db) => sql
                .Append(
                    $"insert into {store.Database.FormatTableNameAndEscape("Entities")} ({store.Database.Escape("Col1")}) values (@value)", 
                    new SqlParameter("@value", SqlDbType.Int) { Value = 1 })));
        }

        [Fact]
        public void IsSafe()
        {
            new SqlCommand("is always safe", (sql, db) => { }).Safe.ShouldBe(true);
        }

        [Fact]
        public void RequiresReprojection()
        {
            new SqlCommand("add some index", "hansoggrethe", (sql, db) => { }).RequiresReprojectionOf.ShouldBe("hansoggrethe");
        }
    }
}