using System;
using System.Data.Common;
using System.IO;
using HybridDb.Queue;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class QueueTableTests : HybridDbTests
    {
        public QueueTableTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Create()
        {
            UseRealTables();

            configuration.UseMessageQueue();

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

            join.ShouldBe(File.ReadAllText(SiblingFile("QueueTableTests_After.sql")));

            database.Tables["messages_old"].ShouldBe(null);
        }
    }
}