using System;
using System.Data.Common;
using System.IO;
using HybridDb.Migrations.BuiltIn;
using HybridDb.Queue;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static HybridDb.Helpers;

namespace HybridDb.Tests.Migrations.BuiltIn
{
    public class RecreateMessageTablesAfterSchemaChanges_Tests : HybridDbTests
    {
        public RecreateMessageTablesAfterSchemaChanges_Tests(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData("RecreateMessageTablesAfterSchemaChanges_Tests_Before_Position.sql")]
        [InlineData("RecreateMessageTablesAfterSchemaChanges_Tests_Before_Version.sql")]
        public void SchemaChanges_1(string beforeFilename)
        {
            UseRealTables();

            Setup(SiblingFile(beforeFilename));

            ResetConfiguration();

            configuration.UseMessageQueue();

            UseMigrations(new InlineMigration(1, ListOf(new RecreateMessageTablesAfterSchemaChanges())));

            TouchStore();

            using var sqlConnection = new SqlConnection(connectionString);

            var server = new Server(new ServerConnection(sqlConnection));

            var parser = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            var databaseName = (string)parser["initial catalog"];
            var database = server.Databases[databaseName];
            var join = string.Join(
                $"{Environment.NewLine}GO{Environment.NewLine}",
                database.Tables["messages"]
                    .EnumScript(new ScriptingOptions
                    {
                        ClusteredIndexes = true,
                        Default = true,
                        Indexes = true,
                        ScriptData = true,
                        DriAll = true,
                        NoFileGroup = true,
                        NoCollation = true
                    }));

            join.ShouldBe(File.ReadAllText(SiblingFile("RecreateMessageTablesAfterSchemaChanges_Tests_After.sql")));

            database.Tables["messages_old"].ShouldBe(null);
        }
    }
}