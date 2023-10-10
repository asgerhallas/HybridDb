using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class AddMigrationIndicesTests : HybridDbTests
    {
        public AddMigrationIndicesTests(ITestOutputHelper output) : base(output) => NoInitialize();

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void AddsColumn(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            Document<Entity>();

            store.Execute(new CreateTable(new DocumentTable("Entities")));

            store.Execute(new AddMigrationIndices());

            var table = new DocumentTable("Entities");

            var rawQuery = store.Database
                .RawQuery<object>($"sp_helpindex '{store.Database.FormatTableName(table.Name)}'")
                .Cast<IDictionary<string, object>>();

            var indices = rawQuery
                .Select(x => (x["index_name"], x["index_keys"]))
                .ToList();

            indices.ShouldContain(("idx_Version", "Version"));
            indices.ShouldContain(("idx_AwaitsReprojection", "AwaitsReprojection"));
        }
    }
}