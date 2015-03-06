using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migration
{
    public class SchemaMigratorTests : IDisposable
    {
        readonly DocumentStore storeWithTempTables;
        readonly DocumentStore storeWithRealTables;
        readonly string uniqueDbName;

        public SchemaMigratorTests()
        {
            // Make a non-temp test database needed for certain tests
            using (var connection = new SqlConnection("data source=.;Integrated Security=True;Pooling=false"))
            {
                connection.Open();

                uniqueDbName = "HybridDbTests_" + Guid.NewGuid().ToString().Replace("-", "_");
                connection.Execute(string.Format(@"
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{0}')
BEGIN
    CREATE DATABASE {0}
END", uniqueDbName));
            }

            storeWithTempTables = DocumentStore.ForTestingWithTempTables("data source=.;Integrated Security=True");
            storeWithRealTables = DocumentStore.ForTestingWithRealTables("data source=.;Integrated Security=True;Initial Catalog=" + uniqueDbName);
        }

        [Fact]
        public void CanRemoveTable()
        {
            storeWithTempTables.Migrate(
                migrator => migrator.AddTable("Entities", "Id UniqueIdentifier")
                                    .RemoveTable("Entities"));

            TempTableExists("Entities").ShouldBe(false);
        }

        [Fact]
        public void CanRenameTable()
        {
            RealTableExists("Entities").ShouldBe(false);
            RealTableExists("NewEntities").ShouldBe(false);

            storeWithRealTables.Migrate(
                migrator => migrator.AddTable("Entities", "Id UniqueIdentifier")
                                    .RenameTable("Entities", "NewEntities"));

            RealTableExists("Entities").ShouldBe(false);
            RealTableExists("NewEntities").ShouldBe(true);
        }

        [Fact]
        public void CanAddColumn()
        {
            storeWithTempTables.Migrate(
                migrator => migrator.AddTable("Entities", "Id UniqueIdentifier")
                                    .AddColumn("Entities", "Property", new SqlBuilder().Append("int")));

            var propertyColumn = GetTempColumn("Entities", "Property");
            propertyColumn.ShouldNotBe(null);
            GetType(propertyColumn.system_type_id).ShouldBe("int");
        }

        [Fact]
        public void CanAddIntColumnWithDefault()
        {
            storeWithTempTables.Migrate(
                migrator => migrator.AddTable("Entities", "Id UniqueIdentifier")
                                    .AddColumn("Entities", new Column("Property", typeof(int), new SqlColumn(DbType.Int32, defaultValue: 10))));

            var propertyColumn = GetTempColumn("Entities", "Property");
            propertyColumn.ShouldNotBe(null);
            GetType(propertyColumn.system_type_id).ShouldBe("int");
        }

        [Fact]
        public void CanAddDateTimeColumnWithDefault()
        {
            storeWithTempTables.Migrate(
                migrator => migrator.AddTable("Entities", "Id UniqueIdentifier")
                                    .AddColumn("Entities", new Column("Property", typeof(DateTimeOffset), new SqlColumn(DbType.DateTimeOffset, defaultValue: DateTimeOffset.Now))));

            var propertyColumn = GetTempColumn("Entities", "Property");
            propertyColumn.ShouldNotBe(null);
            GetType(propertyColumn.system_type_id).ShouldBe("datetimeoffset");
        }

        [Fact]
        public void CanRemoveColumn()
        {
            storeWithTempTables.Migrate(
                migrator => migrator.AddTable("Entities", "Id int", "Property int")
                                    .RemoveColumn("Entities", "Property"));

            GetTempColumn("Entities", "Property").ShouldBe(null);
        }

        [Fact]
        public void CanRenameColumn()
        {
            storeWithRealTables.Migrate(
                migrator => migrator.AddTable("Entities", "Property int"));

            storeWithRealTables.Migrate(
                migrator => migrator.RenameColumn("Entities", "Property", "NewProperty"));

            GetRealColumn("Entities", "Property").ShouldBe(null);
            GetRealColumn("Entities", "NewProperty").ShouldBe(null);
        }

        [Fact]
        public void CanCreateTableAndItsColumns()
        {
            storeWithTempTables.Migrate(migrator => migrator.AddTableAndColumnsAndAssociatedTables(new Table("Entities")));

            TempTableExists("Entities").ShouldBe(true);

            var idColumn = GetTempColumn("Entities", "Id");
            idColumn.ShouldNotBe(null);
            GetType(idColumn.system_type_id).ShouldBe("uniqueidentifier");

            const string sql =
                @"SELECT K.TABLE_NAME,
                  K.COLUMN_NAME,
                  K.CONSTRAINT_NAME
                  FROM tempdb.INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C
                  JOIN tempdb.INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K
                  ON C.TABLE_NAME = K.TABLE_NAME
                  AND C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG
                  AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA
                  AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME
                  WHERE C.CONSTRAINT_TYPE = 'PRIMARY KEY'
                  AND K.COLUMN_NAME = 'Id'";

            var isPrimaryKey = storeWithTempTables.RawQuery<dynamic>(sql).Any();
            isPrimaryKey.ShouldBe(true);

            var etagColumn = GetTempColumn("Entities", "Etag");
            etagColumn.ShouldNotBe(null);
            GetType(etagColumn.system_type_id).ShouldBe("uniqueidentifier");
        }

        [Fact]
        public void CanCreateDocumentTableAndItsExtraColumns()
        {
            storeWithTempTables.Migrate(migrator => migrator.AddTableAndColumnsAndAssociatedTables(new DocumentTable("Entities")));

            TempTableExists("Entities").ShouldBe(true);

            var documentColumn = GetTempColumn("Entities", "Document");
            documentColumn.ShouldNotBe(null);
            GetType(documentColumn.system_type_id).ShouldBe("varbinary");
            documentColumn.max_length.ShouldBe(-1);
        }

        [Fact]
        public void WillQuoteTableAndColumnNamesOnCreation()
        {
            Should.NotThrow(() => storeWithRealTables.Migrate(
                migrator => migrator.AddTable("Create", "By int")));
        }

        [Fact]
        public void MigrationsAreRolledBackOnExceptions()
        {
            //storeWithTempTables.Document<Entity>();
            //storeWithTempTables.MigrateSchemaToMatchConfiguration();

            try
            {
                storeWithTempTables.Migrate(migrator =>
                {
                    migrator.RemoveTable("Entities");
                    throw new Exception();
                });
            }
            catch { }

            TempTableExists("Entities").ShouldBe(true);
        }

        [Fact]
        public void CanCreateColumnWithDefaultValue()
        {
            //storeWithTempTables.Document<Entity>();
            //storeWithTempTables.MigrateSchemaToMatchConfiguration();

            var id = Guid.NewGuid();
            storeWithTempTables.Insert(new Table("Entities"), id, new { });

            storeWithTempTables.Migrate(migrator => migrator
                .AddColumn("Entities", new Column("hest", typeof(string), new SqlColumn(DbType.Int32, defaultValue: 1))));

            var first = storeWithTempTables.RawQuery<int?>("SELECT hest FROM #Entities").First();
            first.ShouldBe(1);
        }

        bool RealTableExists(string name)
        {
            return true;
        }

        bool TempTableExists(string name)
        {
            return true;
        }

        Schema.Column GetTempColumn(string table, string column)
        {
            return null;
        }

        Schema.Column GetRealColumn(string table, string column)
        {
            return null;
        }

        string GetType(int id)
        {
            return ""; //storeWithTempTables.GetType(id);
        }

        public class Entity
        {
            public string Field;
            public Guid Id { get; set; }
            public int Property { get; set; }
            public string StringProp { get; set; }
            public SomeFreakingEnum EnumProp { get; set; }
            public DateTime DateTimeProp { get; set; }
            public Child TheChild { get; set; }

            public class Child
            {
                public double NestedProperty { get; set; }
            }
        }

        public class AnotherEntity
        {
            public Guid Id { get; private set; }
        }

        public enum SomeFreakingEnum
        {
            One,
            Two
        }

        public void Dispose()
        {
            storeWithRealTables.Dispose();
            storeWithTempTables.Dispose();

            SqlConnection.ClearAllPools();

            using (var connection = new SqlConnection("data source=.;Integrated Security=True;Initial Catalog=Master"))
            {
                connection.Open();
                connection.Execute(string.Format("DROP DATABASE {0}", uniqueDbName));
            }

            Transaction.Current.ShouldBe(null);
        }
    }
}