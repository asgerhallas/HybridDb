using System;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HybridDb.Schema;
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
        public void CanRenameProjection()
        {
            var store = new DocumentStore(connectionString);
            var table = new Table("Entities");
            var oldColumn = new UserColumn("Property", new SqlColumn(typeof (int)));
            store.CreateMigrator()
                 .AddTable(table)
                 .AddColumn(table, oldColumn)
                 .Commit()
                 .Dispose();

            store.CreateMigrator()
                 .RenameColumn(table, oldColumn, new UserColumn("Property", new SqlColumn(typeof(int))))
                 .Commit()
                 .Dispose();

            GetColumn("Entities", "Property").ShouldBe(null);
            GetColumn("Entities", "NewProperty").ShouldBe(null);

            connection.Execute("drop table Entities");
        }

        [Fact]
        public void CanRenameTable()
        {
            var store = new DocumentStore(connectionString);
            var table = new Table("Entities");
            store.CreateMigrator().AddTable(table).Commit().Dispose();

            store.CreateMigrator().RenameTable(table, new Table("NewEntities")).Commit().Dispose();
            TableExists("Entities").ShouldBe(false);
            TableExists("NewEntities").ShouldBe(true);

            connection.Execute("drop table NewEntities");
        }

        [Fact]
        public void InitializeFailsIfDatabaseIsNotEmptyWhenNotForTesting()
        {
            TableExists("Cases").ShouldBe(false);

            var store = new DocumentStore(connectionString);
            store.DocumentsFor<Case>();
            store.InitializeDatabase();

            Should.Throw<InvalidOperationException>(() => store.InitializeDatabase());

            connection.Execute("drop table Cases");
        }

        [Fact]
        public void CanCreateRealTables()
        {
            TableExists("Cases").ShouldBe(false);

            var store = new DocumentStore(connectionString);
            store.DocumentsFor<Case>();
            store.InitializeDatabase();

            TableExists("Cases").ShouldBe(true);

            connection.Execute("drop table Cases");
        }

        [Fact]
        public void WillQuoteTableAndColumnNamesOnCreation()
        {
            var store = new DocumentStore(connectionString);
            store.DocumentsFor<Case>("Case").Project(x => x.By);
            Should.NotThrow(() => store.InitializeDatabase());

            connection.Execute("drop table [Case]");
        }

        bool TableExists(string name)
        {
            return connection.Query(string.Format("select OBJECT_ID('{0}') as Result", name)).First().Result != null;
        }

        MigrationWithTempTablesTests.Column GetColumn(string table, string column)
        {
            return connection.Query<MigrationWithTempTablesTests.Column>(string.Format("select * from master.sys.columns where Name = N'{0}' and Object_ID = Object_ID(N'{1}')", column, table))
                             .FirstOrDefault();
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