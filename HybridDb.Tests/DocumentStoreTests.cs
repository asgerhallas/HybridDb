using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using System.Text;
using Dapper;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentStoreTests : IDisposable
    {
        readonly SqlConnection connection;
        readonly string connectionString;

        public DocumentStoreTests()
        {
            connectionString = "data source=.;Initial Catalog=Energy10;Integrated Security=True";
            connection = new SqlConnection(connectionString);
            connection.Open();
        }

        public void Dispose()
        {
            connection.Dispose();
        }

        [Fact]
        public void CanCreateTables()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>();
            store.Initialize();

            TableExists("Entity", temporary: true).ShouldBe(true);
        }

        [Fact]
        public void CanCreateRealTables()
        {
            TableExists("Entity", temporary: false).ShouldBe(false);

            var store = new DocumentStore(connectionString);
            store.ForDocument<Entity>();
            store.Initialize();

            TableExists("Entity", temporary: false).ShouldBe(true);

            connection.Execute("drop table Entity");
        }

        [Fact]
        public void CreatesDefaultColumns()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>();
            store.Initialize();

            var idColumn = GetColumn("Entity", "Id", temporary: true);
            idColumn.ShouldNotBe(null);
            GetType(idColumn.system_type_id).ShouldBe("uniqueidentifier");

            var documentColumn = GetColumn("Entity", "Document", temporary: true);
            documentColumn.ShouldNotBe(null);
            GetType(documentColumn.system_type_id).ShouldBe("varbinary");
            documentColumn.max_length.ShouldBe(-1);
        }

        [Fact]
        public void CanCreateColumnsFromProperties()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Store(x => x.Property);
            store.Initialize();

            var column = GetColumn("Entity", "Property", temporary: true);
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("int");
        }

        [Fact]
        public void CanCreateColumnsFromFields()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Store(x => x.Field);
            store.Initialize();

            var column = GetColumn("Entity", "Field", temporary: true);
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("nvarchar");
            column.max_length.ShouldBe(-1); // -1 is MAX
        }

        [Fact]
        public void CannotInsertWithoutAllValuesProvided()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void CannotUpdateWithoutAllValuesProvided()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void CanInsert()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Store(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var etag = Guid.NewGuid();
            var table = store.Schema.GetTable<Entity>();
            store.Insert(table);

            var row = connection.Query("select * from #Entity").Single();
            ((Guid) row.Id).ShouldBe(id);
            ((Guid) row.Etag).ShouldNotBe(Guid.Empty);
            Encoding.ASCII.GetString((byte[]) row.Document).ShouldBe("asger");
            ((string) row.Field).ShouldBe("Asger");
        }

        [Fact]
        public void CanUpdate()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Store(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var etag = Guid.NewGuid();
            var table = store.Schema.GetTable<Entity>();
            store.Insert(table);

            store.Update(table, new
            {
                Id = id,
                Etag = etag,
                Document = new byte[] { },
                Field = "Lars"
            });

            var row = connection.Query("select * from #Entity").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe("Lars");
        }

        [Fact]
        public void UpdateFailsWhenEtagNotMatch()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Store(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var etag = Guid.NewGuid();
            var table = store.Schema.GetTable<Entity>();
            store.Insert(table);

            Should.Throw<ConcurrencyException>(
                () => store.Update(table, new
                {
                    Id = Guid.NewGuid(),
                    Etag = etag,
                    Document = new byte[] { },
                    Field = "Lars"
                }));
        }
        
        [Fact]
        public void UpdateFailsWhenIdNotMatchAkaObjectDeleted()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Store(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var etag = Guid.NewGuid();
            var table = store.Schema.GetTable<Entity>();
            store.Insert(table);

            Should.Throw<ConcurrencyException>(
                () => store.Update(table, new 
                {
                    Id = Guid.NewGuid(),
                    Etag = etag,
                    Document = new byte[] {},
                    Field = "Lars"
                }));
        }

        [Fact]
        public void CanGet()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Store(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var table = store.Schema.GetTable<Entity>();
            store.Insert(table);

            var entity = store.Get(table, id);
            entity["Id"].ShouldBe(id);
            entity["Field"].ShouldBe("Asger");
        }

        bool TableExists(string name, bool temporary)
        {
            return connection.Query(string.Format("select OBJECT_ID('{0}{1}') as Result",
                                                  temporary ? "tempdb..#" : "",
                                                  name)).First().Result != null;
        }

        Column GetColumn(string table, string column, bool temporary)
        {
            return connection.Query<Column>(string.Format("select * from {0}sys.columns where Name = N'{1}' and Object_ID = Object_ID(N'{2}{3}')",
                                                          temporary ? "tempdb." : "",
                                                          column,
                                                          temporary ? "tempdb..#" : "",
                                                          table))
                             .FirstOrDefault();
        }

        string GetType(int id)
        {
            return connection.Query<string>("select name from sys.types where system_type_id = @id", new {id}).FirstOrDefault();
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
            public Guid Id { get; private set; }
            public int Property { get; set; }
        }
    }
}