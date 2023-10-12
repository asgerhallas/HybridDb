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

            var rawQuery = store.Database
                .RawQuery<object>($"sp_helpindex '{store.Database.FormatTableName(table.Name)}'")
                .Cast<IDictionary<string, object>>();

            var indices = rawQuery
                .Select(x => (x["index_name"], x["index_keys"]))
                .ToList();

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
            configuration.UseMessageQueue();
            Document<Entity>();

            store.Execute(new CreateTable(new QueueTable("QueueTable")));
            store.Execute(new CreateTable(new DocumentTable("Entities")));

            store.Execute(new AddMigrationIndices());

            var queueTable = new QueueTable("QueueTable");
            var documentTable = new DocumentTable("Entities");

            var queueTableIndexQuery = store.Database
                .RawQuery<object>($"sp_helpindex '{store.Database.FormatTableName(queueTable.Name)}'")
                .Cast<IDictionary<string, object>>();

            var queueTableIndices = queueTableIndexQuery
                .Select(x => (x["index_name"], x["index_keys"]))
                .ToList();

            var documentTableIndexQuery = store.Database
                .RawQuery<object>($"sp_helpindex '{store.Database.FormatTableName(documentTable.Name)}'")
                .Cast<IDictionary<string, object>>();

            var documentTableIndices = documentTableIndexQuery 
                .Select(x => (x["index_name"], x["index_keys"]))
                .ToList();

            documentTableIndices .ShouldContain(("idx_Version", "Version"));
            documentTableIndices .ShouldContain(("idx_AwaitsReprojection", "AwaitsReprojection"));

            queueTableIndices.ShouldNotContain(("idx_Version", "Version"));
            queueTableIndices.ShouldNotContain(("idx_AwaitsReprojection", "AwaitsReprojection"));
        }
    }
}