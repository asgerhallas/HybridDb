using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using HybridDb.Queue;
using Microsoft.SqlServer.Management.Smo;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Table = HybridDb.Config.Table;

namespace HybridDb.Tests.Migrations.Commands
{
    public class AddMigrationIndicesTests : HybridDbTests
    {
        public AddMigrationIndicesTests(ITestOutputHelper output) : base(output) => NoInitialize();

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void AddsIndices(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            Document<Entity>();

            store.Execute(new CreateTable(new DocumentTable("Entities")));

            store.Execute(new AddMigrationIndices());

            var table = new DocumentTable("Entities");

            var indices = GetIndexesFor(table);

            indices.ShouldContain(("idx_Version", "Version"));
            indices.ShouldContain(("idx_AwaitsReprojection", "AwaitsReprojection"));
        }

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void IgnoresQueueTables(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());

            Document<OtherEntity>("a");
            configuration.UseMessageQueue(new MessageQueueOptions()
            {
                TableName = "aa"
            });
            Document<Entity>("aaa");

            var otherEntitiesTable = configuration.Tables["a"];
            var queueTable = configuration.Tables["aa"];
            var entitiesTable = configuration.Tables["aaa"];

            store.Execute(new CreateTable(queueTable));
            store.Execute(new CreateTable(entitiesTable));
            store.Execute(new CreateTable(otherEntitiesTable));

            store.Execute(new AddMigrationIndices());

            var queueTableIndices = GetIndexesFor(queueTable);
            var entitiesTableIndices = GetIndexesFor(entitiesTable);
            var otherEntitiesTableIndices = GetIndexesFor(otherEntitiesTable);

            queueTableIndices.ShouldNotContain(("idx_Version", "Version"));
            queueTableIndices.ShouldNotContain(("idx_AwaitsReprojection", "AwaitsReprojection"));

            entitiesTableIndices.ShouldContain(("idx_Version", "Version"));
            entitiesTableIndices.ShouldContain(("idx_AwaitsReprojection", "AwaitsReprojection"));

            otherEntitiesTableIndices.ShouldContain(("idx_Version", "Version"));
            otherEntitiesTableIndices.ShouldContain(("idx_AwaitsReprojection", "AwaitsReprojection"));
        }

        List<(object Name, object Keys)> GetIndexesFor(Table table)
        {
            var rawQuery = store.Database
                .RawQuery<object>($"sp_helpindex '{store.Database.FormatTableName(table.Name)}'")
                .Cast<IDictionary<string, object>>();

            var indices = rawQuery
                .Select(x => (x["index_name"], x["index_keys"]))
                .ToList();

            return indices;
        }
    }
}