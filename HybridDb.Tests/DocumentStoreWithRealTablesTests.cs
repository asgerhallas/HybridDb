using System;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentStoreWithRealTablesTests
    {
        readonly string connectionString;

        public DocumentStoreWithRealTablesTests()
        {
            connectionString = "data source=.;Integrated Security=True";
        }

        [Fact]
        public void CanCreateRealTables()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                TableExists(connection, "Cases").ShouldBe(false);

                var store = new DocumentStore(connectionString);
                store.ForDocument<Case>();
                store.Initialize();

                TableExists(connection, "Cases").ShouldBe(true);

                connection.Execute("drop table Cases");
            }
        }

        [Fact]
        public void WillQuoteTableAndColumnNamesOnCreation()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var store = new DocumentStore(connectionString);
                store.ForDocument<Case>("Case").Projection(x => x.By);
                Should.NotThrow(store.Initialize);

                connection.Execute("drop table [Case]");
            }
        }

        bool TableExists(SqlConnection connection, string name)
        {
            return connection.Query(string.Format("select OBJECT_ID('{0}') as Result", name)).First().Result != null;
        }

        public class Case
        {
            public Guid Id { get; private set; }
            public string By { get; set; }
        }
    }
}