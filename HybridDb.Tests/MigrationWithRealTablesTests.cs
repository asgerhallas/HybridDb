using System;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class MigrationWithRealTablesTests : IDisposable
    {
        readonly SqlConnection connection;
        readonly string connectionString;

        public MigrationWithRealTablesTests()
        {
            connection = new SqlConnection("data source=.;Integrated Security=True");
            connection.Open();
            connection.Execute(@"
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'HybridDbTests')
BEGIN
    CREATE DATABASE HybridDbTests
END");
            connection.Execute("use HybridDbTests");

            connectionString = "data source=.;Integrated Security=True;Initial Catalog=HybridDbTests";
        }

        [Fact]
        public void InitializeFailsIfDatabaseIsNotEmptyWhenNotForTesting()
        {
            TableExists("Cases").ShouldBe(false);

            var store = new DocumentStore(connectionString);
            store.ForDocument<Case>();
            store.Migration.InitializeDatabase();

            Should.Throw<InvalidOperationException>(() => store.Migration.InitializeDatabase());

            connection.Execute("drop table Cases");
        }

        [Fact]
        public void CanCreateRealTables()
        {
            TableExists("Cases").ShouldBe(false);

            var store = new DocumentStore(connectionString);
            store.ForDocument<Case>();
            store.Migration.InitializeDatabase();

            TableExists("Cases").ShouldBe(true);

            connection.Execute("drop table Cases");
        }

        [Fact]
        public void WillQuoteTableAndColumnNamesOnCreation()
        {
            var store = new DocumentStore(connectionString);
            store.ForDocument<Case>("Case").Projection(x => x.By);
            Should.NotThrow(store.Migration.InitializeDatabase);

            connection.Execute("drop table [Case]");
        }

        bool TableExists(string name)
        {
            return connection.Query(string.Format("select OBJECT_ID('{0}') as Result", name)).First().Result != null;
        }

        public class Case
        {
            public Guid Id { get; private set; }
            public string By { get; set; }
        }

        public void Dispose()
        {
            // We don't remove the database again 
            connection.Dispose();
        }
    }
}