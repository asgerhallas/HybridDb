﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class MigrationTests : IDisposable
    {
        readonly DocumentStore store;

        public MigrationTests()
        {
            store = DocumentStore.ForTesting("data source=.;Integrated Security=True");
        }

        [Fact]
        public void ChangesArentPersistedIfExceptionOccours()
        {
            store.ForDocument<Entity>().Projection(x => x.StringProp);
            store.Migration.InitializeDatabase();
            var id1 = Guid.NewGuid();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = id1, Property = 1 });
                session.SaveChanges();
            }

            try
            {
                using (var tx = store.Migration.CreateTransaction())
                {
                    tx.Do<Entity>("Entities",
                                  (entity, projections) =>
                                  {
                                      entity.StringProp = "SomeString";
                                      throw new InvalidOperationException("Error");
                                  });
                    tx.Commit();
                }
            }
            catch { }

            var entity1 = store.Connection.Query<Entity>("SELECT * FROM #Entities").Single();
            entity1.Id.ShouldBe(id1);
            entity1.StringProp.ShouldBe(null);
        }

        [Fact]
        public void CanDeserializeToAnyGivenTypeAndPersistChangesToDocumentWhenDoing()
        {
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();
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

            store.Migration
                .Do<JObject>("Entities", (entity, projections) =>
                {
                    entity["StringProp"] = "SomeString";
                });

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
        public void CanDeserializeToEntityAndPersistChangesToDocumentWhenDoing()
        {
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();
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

            var processedEntities = new List<Entity>();
            store.Migration
                .Do<Entity>("Entities", (entity, projections) =>
                {
                    processedEntities.Add(entity);
                });

            processedEntities.Count.ShouldBe(3);
            processedEntities.ShouldContain(x => x.Id == id1 && x.Property == 1);
            processedEntities.ShouldContain(x => x.Id == id2 && x.Property == 2);
            processedEntities.ShouldContain(x => x.Id == id3 && x.Property == 3);
        }

        [Fact]
        public void CanModifyProjectionWhenDoing() // Note: not in your pants!
        {
            store.ForDocument<Entity>().Projection(x => x.Property);
            store.Migration.InitializeDatabase();
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
            
            store.Migration.Do<Entity>("Entities", (entity, projections) =>
            {
                var projectionValue = (int)projections["Property"];
                projections["Property"] = ++projectionValue;
            });
            
            var result = store.Connection.Query<Entity>("SELECT * FROM #Entities").ToList();
            result.ShouldContain(x => x.Id == id1 && x.Property == 2);
            result.ShouldContain(x => x.Id == id2 && x.Property == 3);
            result.ShouldContain(x => x.Id == id3 && x.Property == 4);
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void CanUpdateProjection()
        {
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();
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
            store.Migration.AddProjection<Entity, int>(x => x.Property);
            
            store.Migration.UpdateProjectionFor<Entity, int>(x => x.Property);
            var result = store.Connection.Query<Entity>("SELECT * FROM #Entities").ToList();
            
            result.ShouldContain(x => x.Id == id1 && x.Property == 1);
            result.ShouldContain(x => x.Id == id2 && x.Property == 2);
            result.ShouldContain(x => x.Id == id3 && x.Property == 3);
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void CanRemoveProjection()
        {
            store.ForDocument<Entity>().Projection(x => x.Property);
            store.Migration.InitializeDatabase();
            
            store.Migration.RemoveProjection<Entity>("Property");
            GetColumn("Entities", "Property").ShouldBe(null);
        }

        [Fact]
        public void CanAddProjection()
        {
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();

            store.Migration.AddProjection<Entity, int>(x => x.Property);
            var propertyColumn = GetColumn("Entities", "Property");
            propertyColumn.ShouldNotBe(null);
            GetType(propertyColumn.system_type_id).ShouldBe("int");
        }

        [Fact]
        public void CanRemoveTable()
        {
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();
            
            store.Migration.RemoveTable("Entities");
            TableExists("Entities").ShouldBe(false);
        }

        [Fact]
        public void CanCreateTable()
        {
            store.Migration.AddTable<Entity>();

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
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();

            TableExists("Entities").ShouldBe(true);
        }

        [Fact]
        public void InitializeCreatesDefaultColumns()
        {
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();

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
            store.ForDocument<Entity>().Projection(x => x.Property).Projection(x => x.TheChild.NestedProperty);
            store.Migration.InitializeDatabase();

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
            store.ForDocument<Entity>().Projection(x => x.Field);
            store.Migration.InitializeDatabase();

            var column = GetColumn("Entities", "Field");
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("nvarchar");
            column.max_length.ShouldBe(-1); // -1 is MAX
        }

        [Fact]
        public void InitializeCreatesIdColumnAsPrimaryKey()
        {
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();

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

            var isPrimaryKey = store.Connection.Query(sql).Any();
            isPrimaryKey.ShouldBe(true);
        }

        [Fact]
        public void InitializeDoesNotFailIfDatabaseIsNotEmptyWhenForTesting()
        {
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();

            Should.NotThrow(() => store.Migration.InitializeDatabase());
        }

        [Fact]
        public void CanCreateMissingTables()
        {
            store.ForDocument<Entity>();
            store.Migration.InitializeDatabase();

            store.ForDocument<AnotherEntity>();
            store.Migration.InitializeDatabase();

            TableExists("AnotherEntities").ShouldBe(true);
        }

        bool TableExists(string name)
        {
            return store.Connection.Query(string.Format("select OBJECT_ID('tempdb..#{0}') as Result", name)).First().Result != null;
        }

        Column GetColumn(string table, string column)
        {
            return store.Connection.Query<Column>(string.Format("select * from tempdb.sys.columns where Name = N'{0}' and Object_ID = Object_ID(N'tempdb..#{1}')", column, table))
                        .FirstOrDefault();
        }

        string GetType(int id)
        {
            return store.Connection.Query<string>("select name from sys.types where system_type_id = @id", new { id }).FirstOrDefault();
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
        }
    }
}