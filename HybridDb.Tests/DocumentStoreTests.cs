using System;
using System.Data.SqlClient;
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
            connectionString = "data source=.;Integrated Security=True";
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
            store.ForDocument<Entity>().Projection(x => x.Property);
            store.Initialize();

            var column = GetColumn("Entity", "Property", temporary: true);
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("int");
        }

        [Fact]
        public void CanCreateColumnsFromFields()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Projection(x => x.Field);
            store.Initialize();

            var column = GetColumn("Entity", "Field", temporary: true);
            column.ShouldNotBe(null);
            GetType(column.system_type_id).ShouldBe("nvarchar");
            column.max_length.ShouldBe(-1); // -1 is MAX
        }

        [Fact]
        public void CanInsert()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Projection(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new[] { (byte)'a', (byte)'s', (byte)'g', (byte)'e', (byte)'r' }, new { Field = "Asger" });

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
            store.ForDocument<Entity>().Projection(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, new[] { (byte)'a', (byte)'s', (byte)'g', (byte)'e', (byte)'r' }, new { Field = "Asger" });

            store.Update(table, id, etag, new byte[] { }, new { Field = "Lars" });

            var row = connection.Query("select * from #Entity").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe("Lars");
        }

        [Fact]
        public void UpdateFailsWhenEtagNotMatch()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Projection(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new[] { (byte)'a', (byte)'s', (byte)'g', (byte)'e', (byte)'r' }, new { Field = "Asger" });

            Should.Throw<ConcurrencyException>(() => store.Update(table, id, Guid.NewGuid(), new byte[] {}, new {Field = "Lars"}));
        }
        
        [Fact]
        public void UpdateFailsWhenIdNotMatchAkaObjectDeleted()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Projection(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var etag = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new[] { (byte)'a', (byte)'s', (byte)'g', (byte)'e', (byte)'r' }, new { Field = "Asger" });

            Should.Throw<ConcurrencyException>(() => store.Update(table, Guid.NewGuid(), etag, new byte[] {}, new { Field = "Lars" }));
        }

        [Fact]
        public void CanGet()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Projection(x => x.Field);
            store.Initialize();

            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var document = new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'};
            var etag = store.Insert(table, id, document, new { Field = "Asger" });

            var row = store.Get(table, id);
            row[table.IdColumn].ShouldBe(id);
            row[table.EtagColumn].ShouldBe(etag);
            row[table.DocumentColumn].ShouldBe(document);
            row[table["Field"]].ShouldBe("Asger");
        }

        [Fact]
        public void CanDelete()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>();
            store.Initialize();

            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, new byte[0], new { });

            store.Delete(table, id, etag);

            connection.Query("select * from #Entity").Count().ShouldBe(0);
        }

        [Fact]
        public void DeleteFailsWhenEtagNotMatch()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>();
            store.Initialize();

            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, new byte[0], new { });

            Should.Throw<ConcurrencyException>(() => store.Delete(table, id, Guid.NewGuid()));
        }

        [Fact]
        public void DeleteFailsWhenIdNotMatchAkaDocumentAlreadyDeleted()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>();
            store.Initialize();

            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, new byte[0], new { });

            Should.Throw<ConcurrencyException>(() => store.Delete(table, Guid.NewGuid(), etag));
        }

        [Fact]
        public void CanBatchCommandsAndGetEtag()
        {
            var store = DocumentStore.ForTesting(connection);
            store.ForDocument<Entity>().Projection(x => x.Field);
            store.Initialize();

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Execute(new InsertCommand(table, id1, new byte[0], new { Field = "A" }),
                                     new InsertCommand(table, id2, new byte[0], new { Field = "B" }));

            var rows = connection.Query<Guid>("select Etag from #Entity order by Field").ToList();
            rows.Count.ShouldBe(2);
            rows[0].ShouldBe(etag);
            rows[1].ShouldBe(etag);
        }

        [Fact]
        public void InitializeIsThreadSafe()
        {
            throw new NotImplementedException();
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