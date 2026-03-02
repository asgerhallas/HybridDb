using System;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
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
            var table = new Table("Entities", new Column<int>("Col1"));
            store.Execute(new CreateTable(table));
            store.Execute(new AddColumn(table.Name, new Column<int>("Col2")));

            store.Execute(new SqlCommand("add some index", (sql, db) => sql
                .Append($"alter table {table} add {new Column<int>("Col3")} int")));
        }

        [Fact]
        public void CanUseParameters()
        {
            var table = new Table("Entities", new Column<int>("Col1"));
            store.Execute(new CreateTable(table));
            store.Execute(new AddColumn(table.Name, new Column<int>("Col2")));

            var value = 1;

            store.Execute(new SqlCommand("add some data", (sql, db) => sql
                .Append(
                    $"insert into {table} ({new Column<int>("Col1")}) values ({value})")));
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