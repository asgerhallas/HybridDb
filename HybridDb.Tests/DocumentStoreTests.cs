using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dapper;
using HybridDb.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentStoreTests : IDisposable
    {
        readonly DocumentStore store;

        public DocumentStoreTests()
        {
            store = DocumentStore.ForTesting("data source=.;Integrated Security=True");
            store.ForDocument<Entity>()
                .Projection(x => x.Field)
                .Projection(x => x.Property)
                .Projection(x => x.TheChild.NestedProperty)
                .Projection(x => x.StringProp)
                .Projection(x => x.DateTimeProp)
                .Projection(x => x.EnumProp);
            store.Initialize();
        }

        public void Dispose()
        {
            store.Dispose();
        }

        [Fact]
        public void CanCreateTables()
        {
            TableExists("Entities").ShouldBe(true);
        }

        [Fact]
        public void CreatesDefaultColumns()
        {
            var idColumn = GetColumn("Entities", "Id");
            idColumn.ShouldNotBe(null);
            GetType(idColumn.system_type_id).ShouldBe("uniqueidentifier");

            var documentColumn = GetColumn("Entities", "Document");
            documentColumn.ShouldNotBe(null);
            GetType(documentColumn.system_type_id).ShouldBe("varbinary");
            documentColumn.max_length.ShouldBe(-1);
        }

        [Fact]
        public void CanCreateColumnsFromProperties()
        {
            var column = GetColumn("Entities", "Property");
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("int");

            var nestedcolumn = GetColumn("Entities", "TheChildNestedProperty");
            nestedcolumn.ShouldNotBe(null);
            GetType(nestedcolumn.system_type_id).ShouldBe("float");
        }

        [Fact]
        public void IdColumnsIsCreatedAsPrimaryKey()
        {
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
        public void CanCreateColumnsFromFields()
        {
            var column = GetColumn("Entities", "Field");
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("nvarchar");
            column.max_length.ShouldBe(-1); // -1 is MAX
        }

        [Fact]
        public void CanInsert()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'}, new {Field = "Asger"});

            var row = store.Connection.Query("select * from #Entities").Single();
            ((Guid) row.Id).ShouldBe(id);
            ((Guid) row.Etag).ShouldNotBe(Guid.Empty);
            Encoding.ASCII.GetString((byte[]) row.Document).ShouldBe("asger");
            ((string) row.Field).ShouldBe("Asger");
        }

        [Fact]
        public void CanUpdate()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'}, new {Field = "Asger"});

            store.Update(table, id, etag, new byte[] {}, new {Field = "Lars"});

            var row = store.Connection.Query("select * from #Entities").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe("Lars");
        }

        [Fact]
        public void CanUpdatePessimistically()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new[] { (byte)'a', (byte)'s', (byte)'g', (byte)'e', (byte)'r' }, new { Field = "Asger" });

            Should.NotThrow(() => store.Update(table, id, Guid.NewGuid(), new byte[] { }, new { Field = "Lars" }, lastWriteWins: true));
        }

        [Fact]
        public void UpdateFailsWhenEtagNotMatch()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'}, new {Field = "Asger"});

            Should.Throw<ConcurrencyException>(() => store.Update(table, id, Guid.NewGuid(), new byte[] {}, new {Field = "Lars"}));
        }

        [Fact]
        public void UpdateFailsWhenIdNotMatchAkaObjectDeleted()
        {
            var id = Guid.NewGuid();
            var etag = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'}, new {Field = "Asger"});

            Should.Throw<ConcurrencyException>(() => store.Update(table, Guid.NewGuid(), etag, new byte[] {}, new {Field = "Lars"}));
        }

        [Fact]
        public void CanGet()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var document = new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'};
            var etag = store.Insert(table, id, document, new {Field = "Asger"});

            var row = store.Get(table, id);
            row[table.IdColumn].ShouldBe(id);
            row[table.EtagColumn].ShouldBe(etag);
            row[table.DocumentColumn].ShouldBe(document);
            row[table["Field"]].ShouldBe("Asger");
        }

        [Fact]
        public void CanQueryWithProjectionToNestedProperty()
        {
            var id1 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag1 = store.Insert(table, id1, new byte[0], new { TheChildNestedProperty = 9.8d });

            QueryStats stats;
            var rows = store.Query<ProjectionWithNestedProperty>(table, out stats).ToList();

            rows.Single().TheChildNestedProperty.ShouldBe(9.8d);
        }

        [Fact]
        public void CanQueryAndReturnFullDocuments()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var document = new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'};
            var etag1 = store.Insert(table, id1, document, new {Field = "Asger"});
            var etag2 = store.Insert(table, id2, document, new {Field = "Hans"});
            store.Insert(table, id3, document, new {Field = "Bjarne"});

            QueryStats stats;
            var rows = store.Query(table, out stats, where: "Field != @name", parameters: new { name = "Bjarne" }).ToList();

            rows.Count().ShouldBe(2);
            var first = rows.Single(x => (Guid) x[table.IdColumn] == id1);
            first[table.EtagColumn].ShouldBe(etag1);
            first[table.DocumentColumn].ShouldBe(document);
            first[table["Field"]].ShouldBe("Asger");

            var second = rows.Single(x => (Guid)x[table.IdColumn] == id2);
            second[table.IdColumn].ShouldBe(id2);
            second[table.EtagColumn].ShouldBe(etag2);
            second[table.DocumentColumn].ShouldBe(document);
            second[table["Field"]].ShouldBe("Hans");
        }

        [Fact]
        public void CanQueryAndReturnAnonymousProjections()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var document = new[] { (byte)'a', (byte)'s', (byte)'g', (byte)'e', (byte)'r' };
            store.Insert(table, id, document, new { Field = "Asger" });

            var t = new {Field = ""};

            QueryStats stats = null;
            var methodInfo = (from method in store.GetType().GetMethods()
                              where method.Name == "Query" && method.IsGenericMethod
                              select method).Single().MakeGenericMethod(t.GetType());

            var rows = (IEnumerable<dynamic>)methodInfo.Invoke(store,
                new object[] {  table, stats, null, "Field = @name", 0, 0, "", new {name = "Asger"} });

            rows.Count().ShouldBe(1);
            Assert.Equal("Asger", rows.Single().Field);
        }

        [Fact]
        public void CanDelete()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, new byte[0], new {});

            store.Delete(table, id, etag);

            store.Connection.Query("select * from #Entities").Count().ShouldBe(0);
        }

        [Fact]
        public void CanDeletePessimistically()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new byte[0], new { });

            Should.NotThrow(() => store.Delete(table, id, Guid.NewGuid(), lastWriteWins: true));
        }

        [Fact]
        public void DeleteFailsWhenEtagNotMatch()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new byte[0], new {});

            Should.Throw<ConcurrencyException>(() => store.Delete(table, id, Guid.NewGuid()));
        }

        [Fact]
        public void DeleteFailsWhenIdNotMatchAkaDocumentAlreadyDeleted()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, new byte[0], new {});

            Should.Throw<ConcurrencyException>(() => store.Delete(table, Guid.NewGuid(), etag));
        }

        [Fact]
        public void CanBatchCommandsAndGetEtag()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Execute(new InsertCommand(table, id1, new byte[0], new {Field = "A"}),
                                     new InsertCommand(table, id2, new byte[0], new {Field = "B"}));

            var rows = store.Connection.Query<Guid>("select Etag from #Entities order by Field").ToList();
            rows.Count.ShouldBe(2);
            rows[0].ShouldBe(etag);
            rows[1].ShouldBe(etag);
        }

        [Fact]
        public void BatchesAreTransactional()
        {
            var id1 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etagThatMakesItFail = Guid.NewGuid();
            try
            {
                store.Execute(new InsertCommand(table, id1, new byte[0], new { Field = "A" }),
                              new UpdateCommand(table, id1, etagThatMakesItFail, new byte[0], new { Field = "B" }, false));
            }
            catch (ConcurrencyException)
            {
                // ignore the exception and ensure that nothing was inserted
            }

            store.Connection.Query("select * from #Entities").Count().ShouldBe(0);
        }


        [Fact]
        public void WillNotCreateSchemaIfItAlreadyExists()
        {
            var store1 = DocumentStore.ForTesting("data source=.;Integrated Security=True");
            store1.ForDocument<Case>().Projection(x => x.By);
            store1.Initialize();

            var store2 = DocumentStore.ForTesting("data source=.;Integrated Security=True");
            store2.ForDocument<Case>().Projection(x => x.By);
            
            Should.NotThrow(store2.Initialize);
        }

        [Fact]
        public void CanSplitLargeCommandBatches()
        {
            var table = store.Configuration.GetTableFor<Entity>();

            var commands = new List<DatabaseCommand>();
            for (int i = 0; i < 2100/4+1; i++)
            {
                commands.Add(new InsertCommand(table, Guid.NewGuid(), new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'}, new {Field = "A"}));
            }

            store.Execute(commands.ToArray());
            store.NumberOfRequests.ShouldBe(2);
        }

        [Fact]
        public void CanStoreAndQueryEnumProjection()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table, id, new byte[0], new {EnumProp = SomeFreakingEnum.Two});

            var result = store.Get(table, id);
            result[table["EnumProp"]].ShouldBe(SomeFreakingEnum.Two.ToString());
        }

        [Fact]
        public void CanStoreAndQueryEnumProjectionToNetType()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table, id, new byte[0], new {EnumProp = SomeFreakingEnum.Two});

            QueryStats stats;
            var result = store.Query<ProjectionWithEnum>(table, out stats).Single();
            result.EnumProp.ShouldBe(SomeFreakingEnum.Two);
        }

        [Fact]
        public void CanStoreAndQueryStringProjection()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table, id, new byte[0], new {StringProp = "Hest"});

            var result = store.Get(table, id);
            result[table["StringProp"]].ShouldBe("Hest");
        }

        [Fact]
        public void CanStoreAndQueryDateTimeProjection()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table, id, new byte[0], new {DateTimeProp = new DateTime(2001, 12, 24, 1, 1, 1)});

            var result = store.Get(table, id);
            result[table["DateTimeProp"]].ShouldBe(new DateTime(2001, 12, 24, 1, 1, 1));
        }

        [Fact]
        public void CanPage()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (int i = 0; i < 10; i++)
                store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = i });

            QueryStats stats;
            var result = store.Query(table, out stats, skip: 2, take: 5, orderby: "Property").ToList();

            result.Count.ShouldBe(5);
            var props = result.Select(x => x[table["Property"]]).ToList();
            props.ShouldContain(2);
            props.ShouldContain(3);
            props.ShouldContain(4);
            props.ShouldContain(5);
            props.ShouldContain(6);
            stats.TotalRows.ShouldBe(10);
        }

        [Fact]
        public void CanTake()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (int i = 0; i < 10; i++)
                store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = i });

            QueryStats stats;
            var result = store.Query(table, out stats, take: 2, orderby: "Property").ToList();

            result.Count.ShouldBe(2);
            var props = result.Select(x => x[table["Property"]]).ToList();
            props.ShouldContain(0);
            props.ShouldContain(1);
            stats.TotalRows.ShouldBe(10);
        }

        [Fact]
        public void CanSkip()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (int i = 0; i < 10; i++)
                store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = i });

            QueryStats stats;
            var result = store.Query(table, out stats, skip: 7, orderby: "Property").ToList();

            result.Count.ShouldBe(3);
            var props = result.Select(x => x[table["Property"]]).ToList();
            props.ShouldContain(7);
            props.ShouldContain(8);
            props.ShouldContain(9);
            stats.TotalRows.ShouldBe(10);
        }

        [Fact]
        public void CanGetTotalRows()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (int i = 0; i < 10; i++)
                store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = i });

            QueryStats stats;
            var result = store.Query(table, out stats, where: "Property >= 5", skip: 2).ToList();

            result.Count.ShouldBe(3);
            stats.TotalRows.ShouldBe(5);
        }

        [Fact]
        public void CanGetTotalRowsWithNoResults()
        {
            var table = store.Configuration.GetTableFor<Entity>();

            QueryStats stats;
            var result = store.Query(table, out stats).ToList();

            result.Count.ShouldBe(0);
            stats.TotalRows.ShouldBe(0);
        }

        [Fact]
        public void CanQueryWithoutWhere()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, Guid.NewGuid(), new byte[0], new {});

            QueryStats stats;
            var result = store.Query(table, out stats).ToList();

            result.Count.ShouldBe(1);
        }

        [Fact]
        public void CanOrderBy()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (int i = 5; i > 0; i--)
                store.Insert(table, Guid.NewGuid(), new byte[0], new { Field = i });

            QueryStats stats;
            var result = store.Query(table, out stats, orderby: "Field").ToList();

            var props = result.Select(x => x[table["Field"]]).ToList();
            props[0].ShouldBe("1");
            props[1].ShouldBe("2");
            props[2].ShouldBe("3");
            props[3].ShouldBe("4");
            props[4].ShouldBe("5");
        }

        [Fact]
        public void CanOrderByDescWhileSkippingAndTaking()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (int i = 5; i > 0; i--)
                store.Insert(table, Guid.NewGuid(), new byte[0], new { Field = i });

            QueryStats stats;
            var result = store.Query(table, out stats, skip: 2, take: 2, orderby: "Field desc").ToList();

            var props = result.Select(x => x[table["Field"]]).ToList();
            props[0].ShouldBe("3");
            props[1].ShouldBe("2");
        }

        [Fact]
        public void FailsIfProjectionQueryOnNonProjectedFieldsOrProperties()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void FailsIfDatabaseSchemaExistsButDoesNotMatchCurrentConfiguration()
        {
            throw new NotImplementedException();
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
            return store.Connection.Query<string>("select name from sys.types where system_type_id = @id", new {id}).FirstOrDefault();
        }

        public class Column
        {
            public string Name { get; set; }
            public int system_type_id { get; set; }
            public int max_length { get; set; }
        }

        public class Case
        {
            public Guid Id { get; private set; }
            public string By { get; set; }
        }

        public class Entity
        {
            public string Field;
            public Guid Id { get; private set; }
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

        public class ProjectionWithNestedProperty
        {
            public double TheChildNestedProperty { get; set; }
        }

        public class ProjectionWithEnum
        {
            public SomeFreakingEnum EnumProp { get; set; }
        }

        public enum SomeFreakingEnum
        {
            One,
            Two
        }
    }
}