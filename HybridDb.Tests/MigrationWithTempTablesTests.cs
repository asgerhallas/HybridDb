using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Schema;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class MigrationWithTempTablesTests : IDisposable
    {
        readonly DocumentStore storeWithTempTables;
        readonly DocumentStore storeWithRealTables;
        readonly string uniqueDbName;

        public MigrationWithTempTablesTests()
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

            storeWithTempTables = DocumentStore.ForTestingWithTempTables("data source=.;Integrated Security=True;Pooling=false");
            storeWithRealTables = new DocumentStore("data source=.;Integrated Security=True;Pooling=false;Initial Catalog=" + uniqueDbName);
        }

        [Fact]
        public void CanCreateTableAndItsColumns()
        {
            storeWithTempTables.Migrate(migrator => migrator.AddTableAndColumns(new Table("Entities")));

            TempTableExists("Entities").ShouldBe(true);

            var idColumn = GetColumn("Entities", "Id");
            idColumn.ShouldNotBe(null);
            GetType(idColumn.system_type_id).ShouldBe("uniqueidentifier");

            var documentColumn = GetColumn("Entities", "Document");
            documentColumn.ShouldNotBe(null);
            GetType(documentColumn.system_type_id).ShouldBe("varbinary");
            documentColumn.max_length.ShouldBe(-1);

            var etagColumn = GetColumn("Entities", "Etag");
            etagColumn.ShouldNotBe(null);
            GetType(etagColumn.system_type_id).ShouldBe("uniqueidentifier");
        }

        [Fact]
        public void CanRemoveTable()
        {
            storeWithTempTables.Migrate(
                migrator => migrator.AddTableAndColumns(new Table("Entities"))
                                    .RemoveTable(new Table("Entities")));

            TempTableExists("Entities").ShouldBe(false);
        }

        [Fact]
        public void CanRenameTable()
        {
            RealTableExists("Entities").ShouldBe(false);
            RealTableExists("NewEntities").ShouldBe(false);

            var table = new Table("Entities");
            storeWithRealTables.Migrate(
                migrator => migrator.AddTableAndColumns(table)
                                    .RenameTable(table, new Table("NewEntities")));

            RealTableExists("Entities").ShouldBe(false);
            RealTableExists("NewEntities").ShouldBe(true);
        }

        [Fact]
        public void CanAddColumn()
        {
            var table = new Table("Entities");
            var column = new UserColumn("Property", new SqlColumn(typeof (int)));

            storeWithTempTables.Migrate(
                migrator => migrator.AddTableAndColumns(table)
                                    .AddColumn(table, column));

            var propertyColumn = GetColumn("Entities", "Property");
            propertyColumn.ShouldNotBe(null);
            GetType(propertyColumn.system_type_id).ShouldBe("int");
        }

        [Fact]
        public void CanRemoveColumn()
        {
            var table = new Table("Entities");
            var column = new UserColumn("Property", new SqlColumn(typeof(int)));

            storeWithTempTables.Migrate(
                migrator => migrator.AddTableAndColumns(table)
                                    .AddColumn(table, column)
                                    .RemoveColumn(table, column));

            GetColumn("Entities", "Property").ShouldBe(null);
        }

        [Fact]
        public void CanDeserializeToEntityAndPersistChangesToDocument()
        {
            storeWithTempTables.DocumentsFor<Entity>();
            storeWithTempTables.InitializeDatabase();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using (var session = storeWithTempTables.OpenSession())
            {
                session.Store(new Entity {Id = id1, Property = 1});
                session.Store(new Entity {Id = id2, Property = 2});
                session.Store(new Entity {Id = id3, Property = 3});
                session.SaveChanges();
            }

            var processedEntities = new List<Entity>();
            storeWithTempTables.Migrate(migrator =>
                                        migrator.Do<Entity>(
                                            new Table("Entities"),
                                            storeWithTempTables.Configuration.Serializer,
                                            (entity, projections) => processedEntities.Add(entity)));

            processedEntities.Count.ShouldBe(3);
            processedEntities.ShouldContain(x => x.Id == id1 && x.Property == 1);
            processedEntities.ShouldContain(x => x.Id == id2 && x.Property == 2);
            processedEntities.ShouldContain(x => x.Id == id3 && x.Property == 3);
        }

        [Fact]
        public void CanDeserializeToAnyGivenTypeAndPersistChangesToDocument()
        {
            storeWithTempTables.DocumentsFor<Entity>();
            storeWithTempTables.InitializeDatabase();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using (var session = storeWithTempTables.OpenSession())
            {
                session.Store(new Entity {Id = id1, Property = 1});
                session.Store(new Entity {Id = id2, Property = 2});
                session.Store(new Entity {Id = id3, Property = 3});
                session.SaveChanges();
            }

            storeWithTempTables.Migrate(migrator =>
                                        migrator.Do<JObject>(
                                            new Table("Entities"),
                                            storeWithTempTables.Configuration.Serializer,
                                            (entity, projections) => { entity["StringProp"] = "SomeString"; }));

            using (var session = storeWithTempTables.OpenSession())
            {
                var entities = session.Query<Entity>().ToList();
                entities.Count().ShouldBe(3);
                entities.ShouldContain(x => x.Id == id1 && x.Property == 1 && x.StringProp == "SomeString");
                entities.ShouldContain(x => x.Id == id2 && x.Property == 2 && x.StringProp == "SomeString");
                entities.ShouldContain(x => x.Id == id3 && x.Property == 3 && x.StringProp == "SomeString");
            }
        }

        [Fact]
        public void CanModifyProjection()
        {
            storeWithTempTables.DocumentsFor<Entity>().Project(x => x.Property);
            storeWithTempTables.InitializeDatabase();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using (var session = storeWithTempTables.OpenSession())
            {
                session.Store(new Entity {Id = id1, Property = 1});
                session.Store(new Entity {Id = id2, Property = 2});
                session.Store(new Entity {Id = id3, Property = 3});
                session.SaveChanges();
            }

            storeWithTempTables.Migrate(migrator =>
                                        migrator.Do<Entity>(
                                            new Table("Entities"),
                                            storeWithTempTables.Configuration.Serializer,
                                            (entity, projections) =>
                                            {
                                                var projectionValue = (int) projections["Property"];
                                                projections["Property"] = ++projectionValue;
                                            }));

            var result = storeWithTempTables.RawQuery<Entity>("SELECT * FROM #Entities").ToList();
            result.ShouldContain(x => x.Id == id1 && x.Property == 2);
            result.ShouldContain(x => x.Id == id2 && x.Property == 3);
            result.ShouldContain(x => x.Id == id3 && x.Property == 4);
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void CanUpdateProjection()
        {
            storeWithTempTables.DocumentsFor<Entity>();
            storeWithTempTables.InitializeDatabase();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using (var session = storeWithTempTables.OpenSession())
            {
                session.Store(new Entity {Id = id1, Property = 1});
                session.Store(new Entity {Id = id2, Property = 2});
                session.Store(new Entity {Id = id3, Property = 3});
                session.SaveChanges();
            }

            storeWithTempTables.Migrate(migrator =>
            {
                var tableToTypeRelation = storeWithTempTables.Configuration.GetTableFor<Entity>();
                tableToTypeRelation.Project(x => x.Property);

                migrator.AddColumn(tableToTypeRelation.Table, new UserColumn("Property", new SqlColumn(typeof (int))));
                migrator.UpdateProjectionColumnsFromDocument(tableToTypeRelation, storeWithTempTables.Configuration.Serializer);
            });

            var result = storeWithTempTables.RawQuery<Entity>("SELECT * FROM #Entities").ToList();

            result.ShouldContain(x => x.Id == id1 && x.Property == 1);
            result.ShouldContain(x => x.Id == id2 && x.Property == 2);
            result.ShouldContain(x => x.Id == id3 && x.Property == 3);
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void InitializeCreatesTables()
        {
            storeWithTempTables.DocumentsFor<Entity>();
            storeWithTempTables.InitializeDatabase();

            TempTableExists("Entities").ShouldBe(true);
        }

        [Fact]
        public void InitializeCreatesDefaultColumns()
        {
            storeWithTempTables.DocumentsFor<Entity>();
            storeWithTempTables.InitializeDatabase();

            var idColumn = GetColumn("Entities", "Id");
            idColumn.ShouldNotBe(null);
            GetType(idColumn.system_type_id).ShouldBe("uniqueidentifier");

            var documentColumn = GetColumn("Entities", "Document");
            documentColumn.ShouldNotBe(null);
            GetType(documentColumn.system_type_id).ShouldBe("varbinary");
            documentColumn.max_length.ShouldBe(-1);
        }

        [Fact]
        public void InitializeCreatesColumnsFromProperties()
        {
            storeWithTempTables.DocumentsFor<Entity>().Project(x => x.Property).Project(x => x.TheChild.NestedProperty);
            storeWithTempTables.InitializeDatabase();

            var column = GetColumn("Entities", "Property");
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("int");

            var nestedcolumn = GetColumn("Entities", "TheChildNestedProperty");
            nestedcolumn.ShouldNotBe(null);
            GetType(nestedcolumn.system_type_id).ShouldBe("float");
        }

        [Fact]
        public void InitializeCreatesColumnsFromFields()
        {
            storeWithTempTables.DocumentsFor<Entity>().Project(x => x.Field);
            storeWithTempTables.InitializeDatabase();

            var column = GetColumn("Entities", "Field");
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("nvarchar");
            column.max_length.ShouldBe(-1); // -1 is MAX
        }

        [Fact]
        public void InitializeCreatesIdColumnAsPrimaryKey()
        {
            storeWithTempTables.DocumentsFor<Entity>();
            storeWithTempTables.InitializeDatabase();

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

            var isPrimaryKey = storeWithTempTables.RawQuery(sql).Any();
            isPrimaryKey.ShouldBe(true);
        }

        [Fact]
        public void InitializeDoesNotFailIfDatabaseIsNotEmptyWhenForTesting()
        {
            storeWithTempTables.DocumentsFor<Entity>();
            storeWithTempTables.InitializeDatabase();

            Should.NotThrow(() => storeWithTempTables.InitializeDatabase());
        }

        [Fact]
        public void CanCreateMissingTables()
        {
            storeWithTempTables.DocumentsFor<Entity>();
            storeWithTempTables.InitializeDatabase();

            storeWithTempTables.DocumentsFor<AnotherEntity>();
            storeWithTempTables.InitializeDatabase();

            TempTableExists("AnotherEntities").ShouldBe(true);
        }

        [Fact]
        public void MigrationsAreRolledBackOnExceptions()
        {
            storeWithTempTables.DocumentsFor<Entity>().Project(x => x.StringProp);
            storeWithTempTables.InitializeDatabase();
            var id1 = Guid.NewGuid();

            using (var session = storeWithTempTables.OpenSession())
            {
                session.Store(new Entity {Id = id1, Property = 1});
                session.SaveChanges();
            }

            try
            {
                storeWithTempTables.Migrate(migrator =>
                                            migrator.Do<Entity>(
                                                new Table("Entities"),
                                                storeWithTempTables.Configuration.Serializer,
                                                (entity, projections) =>
                                                {
                                                    entity.StringProp = "SomeString";
                                                    throw new Exception("Error");
                                                }));
            }
            catch {}

            var entity1 = storeWithTempTables.RawQuery<Entity>("SELECT * FROM #Entities").Single();
            entity1.Id.ShouldBe(id1);
            entity1.StringProp.ShouldBe(null);
        }

        bool RealTableExists(string name)
        {
            return storeWithRealTables.RawQuery(string.Format("select OBJECT_ID('{0}') as Result", name)).First().Result != null;
        }

        bool TempTableExists(string name)
        {
            return storeWithTempTables.RawQuery(string.Format("select OBJECT_ID('tempdb..#{0}') as Result", name)).First().Result != null;
        }

        Column GetColumn(string table, string column)
        {
            return
                storeWithTempTables.RawQuery<Column>(string.Format("select * from tempdb.sys.columns where Name = N'{0}' and Object_ID = Object_ID(N'tempdb..#{1}')", column, table))
                                   .FirstOrDefault();
        }

        string GetType(int id)
        {
            return storeWithTempTables.RawQuery<string>("select name from sys.types where system_type_id = @id", new {id}).FirstOrDefault();
        }

        public class Column
        {
            public string Name { get; set; }
            public int system_type_id { get; set; }
            public int max_length { get; set; }
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

            using (var connection = new SqlConnection("data source=.;Integrated Security=True;Pooling=false;Initial Catalog=Master"))
            {
                connection.Open();
                connection.Execute(string.Format("DROP DATABASE {0}", uniqueDbName));
            }

            Transaction.Current.ShouldBe(null);
        }
    }
}