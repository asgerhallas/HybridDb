using System;
using System.Collections.Generic;
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
        readonly DocumentStore store;

        public MigrationWithTempTablesTests()
        {
            store = DocumentStore.ForTestingWithTempTables("data source=.;Integrated Security=True");
        }

        [Fact]
        public void ChangesAreNotPersistedOnExceptions()
        {
            store.DocumentsFor<Entity>().Project(x => x.StringProp);
            store.InitializeDatabase();
            var id1 = Guid.NewGuid();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id1, Property = 1 });
                session.SaveChanges();
            }

            try
            {
                using (var tx = store.CreateMigrator())
                {
                    tx.Do<Entity>(new Table("Entities"),
                                  store.Configuration.Serializer,
                                  (entity, projections) =>
                                  {
                                      entity.StringProp = "SomeString";
                                      throw new Exception("Error");
                                  });
                    tx.Commit();
                }
            }
            catch { }

            var entity1 = store.RawQuery<Entity>("SELECT * FROM #Entities").Single();
            entity1.Id.ShouldBe(id1);
            entity1.StringProp.ShouldBe(null);
        }

        [Fact]
        public void CanDeserializeToAnyGivenTypeAndPersistChangesToDocument()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id1, Property = 1 });
                session.Store(new Entity { Id = id2, Property = 2 });
                session.Store(new Entity { Id = id3, Property = 3 });
                session.SaveChanges();
            }

            store.CreateMigrator()
                 .Do<JObject>(new Table("Entities"),
                              store.Configuration.Serializer,
                              (entity, projections) => { entity["StringProp"] = "SomeString"; })
                 .Commit()
                 .Dispose();

            using(var session = store.OpenSession())
            {
                var entities = session.Query<Entity>().ToList();
                entities.Count().ShouldBe(3);
                entities.ShouldContain(x => x.Id == id1 && x.Property == 1 && x.StringProp == "SomeString");
                entities.ShouldContain(x => x.Id == id2 && x.Property == 2 && x.StringProp == "SomeString");
                entities.ShouldContain(x => x.Id == id3 && x.Property == 3 && x.StringProp == "SomeString");
            }
        }

        [Fact]
        public void CanDeserializeToEntityAndPersistChangesToDocument()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity {Id = id1, Property = 1});
                session.Store(new Entity {Id = id2, Property = 2});
                session.Store(new Entity {Id = id3, Property = 3});
                session.SaveChanges();
            }

            var processedEntities = new List<Entity>();
            store.CreateMigrator()
                 .Do<Entity>(new Table("Entities"),
                             store.Configuration.Serializer,
                             (entity, projections) => processedEntities.Add(entity))
                 .Commit()
                 .Dispose();

            processedEntities.Count.ShouldBe(3);
            processedEntities.ShouldContain(x => x.Id == id1 && x.Property == 1);
            processedEntities.ShouldContain(x => x.Id == id2 && x.Property == 2);
            processedEntities.ShouldContain(x => x.Id == id3 && x.Property == 3);
        }

        [Fact]
        public void CanModifyProjection()
        {
            store.DocumentsFor<Entity>().Project(x => x.Property);
            store.InitializeDatabase();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id1, Property = 1 });
                session.Store(new Entity { Id = id2, Property = 2 });
                session.Store(new Entity { Id = id3, Property = 3 });
                session.SaveChanges();
            }

            store.CreateMigrator()
                 .Do<Entity>(new Table("Entities"),
                             store.Configuration.Serializer,
                             (entity, projections) =>
                             {
                                 var projectionValue = (int) projections["Property"];
                                 projections["Property"] = ++projectionValue;
                             });
            
            var result = store.RawQuery<Entity>("SELECT * FROM #Entities").ToList();
            result.ShouldContain(x => x.Id == id1 && x.Property == 2);
            result.ShouldContain(x => x.Id == id2 && x.Property == 3);
            result.ShouldContain(x => x.Id == id3 && x.Property == 4);
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void CanUpdateProjection()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            using(var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id1, Property = 1 });
                session.Store(new Entity { Id = id2, Property = 2 });
                session.Store(new Entity { Id = id3, Property = 3 });
                session.SaveChanges();
            }

            using (var migrator = store.CreateMigrator())
            {
                var tableToTypeRelation = store.Configuration.GetTableFor<Entity>();
                tableToTypeRelation.Project(x => x.Property);

                migrator.AddColumn(tableToTypeRelation.Table, new UserColumn("Property", new SqlColumn(typeof (int))));
                migrator.UpdateProjectionColumnsFromDocument(tableToTypeRelation, store.Configuration.Serializer);
                migrator.Commit();
            }

            var result = store.RawQuery<Entity>("SELECT * FROM #Entities").ToList();
            
            result.ShouldContain(x => x.Id == id1 && x.Property == 1);
            result.ShouldContain(x => x.Id == id2 && x.Property == 2);
            result.ShouldContain(x => x.Id == id3 && x.Property == 3);
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void CanRemoveColumn()
        {
            store.DocumentsFor<Entity>().Project(x => x.Property);
            store.InitializeDatabase();

            store.CreateMigrator()
                 .RemoveColumn(new Table("Entities"),
                               new UserColumn("Property", new SqlColumn()))
                 .Commit()
                 .Dispose();

            GetColumn("Entities", "Property").ShouldBe(null);
        }

        [Fact]
        public void CanAddColumn()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();

            store.CreateMigrator()
                 .AddColumn(new Table("Entities"),
                            new UserColumn("Property", new SqlColumn(typeof (int))))
                 .Commit()
                 .Dispose();
            
            var propertyColumn = GetColumn("Entities", "Property");
            propertyColumn.ShouldNotBe(null);
            GetType(propertyColumn.system_type_id).ShouldBe("int");
        }

        [Fact]
        public void CanRemoveTable()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();

            store.CreateMigrator().RemoveTable(new Table("Entities"));
            TableExists("Entities").ShouldBe(false);
        }

        [Fact]
        public void CanCreateTable()
        {
            store.CreateMigrator().AddTable(new Table("Entities")).Commit().Dispose();

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
        public void InitializeCreatesTables()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();

            TableExists("Entities").ShouldBe(true);
        }

        [Fact]
        public void InitializeCreatesDefaultColumns()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();

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
            store.DocumentsFor<Entity>().Project(x => x.Property).Project(x => x.TheChild.NestedProperty);
            store.InitializeDatabase();

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
            store.DocumentsFor<Entity>().Project(x => x.Field);
            store.InitializeDatabase();

            var column = GetColumn("Entities", "Field");
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("nvarchar");
            column.max_length.ShouldBe(-1); // -1 is MAX
        }

        [Fact]
        public void InitializeCreatesIdColumnAsPrimaryKey()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();

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

            var isPrimaryKey = store.RawQuery(sql).Any();
            isPrimaryKey.ShouldBe(true);
        }

        [Fact]
        public void InitializeDoesNotFailIfDatabaseIsNotEmptyWhenForTesting()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();

            Should.NotThrow(() => store.InitializeDatabase());
        }

        [Fact]
        public void CanCreateMissingTables()
        {
            store.DocumentsFor<Entity>();
            store.InitializeDatabase();

            store.DocumentsFor<AnotherEntity>();
            store.InitializeDatabase();

            TableExists("AnotherEntities").ShouldBe(true);
        }

        bool TableExists(string name)
        {
            return store.RawQuery(string.Format("select OBJECT_ID('tempdb..#{0}') as Result", name)).First().Result != null;
        }

        Column GetColumn(string table, string column)
        {
            return store.RawQuery<Column>(string.Format("select * from tempdb.sys.columns where Name = N'{0}' and Object_ID = Object_ID(N'tempdb..#{1}')", column, table))
                        .FirstOrDefault();
        }

        string GetType(int id)
        {
            return store.RawQuery<string>("select name from sys.types where system_type_id = @id", new { id }).FirstOrDefault();
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
            store.Dispose();
            Transaction.Current.ShouldBe(null);
        }
    }
}