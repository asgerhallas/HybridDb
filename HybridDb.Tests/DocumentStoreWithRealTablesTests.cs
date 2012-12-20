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
                TableExists(connection, "Entities").ShouldBe(false);

                var store = new DocumentStore(connectionString);
                store.ForDocument<Entity>();
                store.Initialize();

                TableExists(connection, "Entities").ShouldBe(true);

                connection.Execute("drop table Entities");
            }
        }

        bool TableExists(SqlConnection connection, string name)
        {
            return connection.Query(string.Format("select OBJECT_ID('{0}') as Result", name)).First().Result != null;
        }

        public class Entity
        {
            public Guid Id { get; private set; }
        }
    }
}