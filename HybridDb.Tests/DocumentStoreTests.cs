using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dapper;
using HybridDb.Commands;
using HybridDb.Schema;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentStoreTests : IDisposable
    {
        readonly DocumentStore store;
        readonly byte[] documentAsByteArray;

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
            store.Migration.InitializeDatabase();

            documentAsByteArray = new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'};
        }

        public void Dispose()
        {
            store.Dispose();
        }

        [Fact]
        public void CanInsert()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, documentAsByteArray, new { Field = "Asger" });

            var row = store.Connection.Query("select * from #Entities").Single();
            ((Guid) row.Id).ShouldBe(id);
            ((Guid) row.Etag).ShouldNotBe(Guid.Empty);
            Encoding.ASCII.GetString((byte[]) row.Document).ShouldBe("asger");
            ((string) row.Field).ShouldBe("Asger");
        }

        [Fact]
        public void CanInsertDynamically()
        {
            var id = Guid.NewGuid();
            store.Insert(new Table("Entities"), id,
                         documentAsByteArray,
                         new {Field = "Asger"});

            var row = store.Connection.Query("select * from #Entities").Single();
            ((Guid)row.Id).ShouldBe(id);
            ((Guid)row.Etag).ShouldNotBe(Guid.Empty);
            Encoding.ASCII.GetString((byte[])row.Document).ShouldBe("asger");
            ((string)row.Field).ShouldBe("Asger");
        }

        [Fact]
        public void CanInsertNullsDynamically()
        {
            store.Insert(new Table("Entities"),
                         Guid.NewGuid(),
                         documentAsByteArray,
                         new Dictionary<string, object> {{"Field", null}});

            var row = store.Connection.Query("select * from #Entities").Single();
            ((string) row.Field).ShouldBe(null);
        }

        [Fact]
        public void CanUpdate()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, documentAsByteArray, new {Field = "Asger"});

            store.Update(table, id, etag, new byte[] {}, new {Field = "Lars"});

            var row = store.Connection.Query("select * from #Entities").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe("Lars");
        }

        [Fact]
        public void CanUpdateDynamically()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, documentAsByteArray, new {Field = "Asger"});

            store.Update(new Table("Entities"), id, etag, new byte[] { }, new Dictionary<string, object> { { "Field", null }, { "StringProp", "Lars" } });

            var row = store.Connection.Query("select * from #Entities").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe(null);
            ((string) row.StringProp).ShouldBe("Lars");
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
            store.Insert(table, id, documentAsByteArray, new {Field = "Asger"});

            Should.Throw<ConcurrencyException>(() => store.Update(table, id, Guid.NewGuid(), new byte[] {}, new {Field = "Lars"}));
        }

        [Fact]
        public void UpdateFailsWhenIdNotMatchAkaObjectDeleted()
        {
            var id = Guid.NewGuid();
            var etag = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id, documentAsByteArray, new {Field = "Asger"});

            Should.Throw<ConcurrencyException>(() => store.Update(table, Guid.NewGuid(), etag, new byte[] {}, new {Field = "Lars"}));
        }

        [Fact]
        public void CanGet()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, documentAsByteArray, new {Field = "Asger"});

            var row = store.Get(table, id);
            row[table.IdColumn].ShouldBe(id);
            row[table.EtagColumn].ShouldBe(etag);
            row[table.DocumentColumn].ShouldBe(documentAsByteArray);
            row[table["Field"]].ShouldBe("Asger");
        }

        [Fact]
        public void CanGetDynamically()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table, id, documentAsByteArray, new { Field = "Asger" });

            var row = store.Get(new Table("Entities"), id);
            row[table.IdColumn].ShouldBe(id);
            row[table.EtagColumn].ShouldBe(etag);
            row[table.DocumentColumn].ShouldBe(documentAsByteArray);
            row[table["Field"]].ShouldBe("Asger");
        }

        [Fact]
        public void CanQueryWithProjectionToNestedProperty()
        {
            var id1 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id1, new byte[0], new { TheChildNestedProperty = 9.8d });

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
            var etag1 = store.Insert(table, id1, documentAsByteArray, new {Field = "Asger"});
            var etag2 = store.Insert(table, id2, documentAsByteArray, new {Field = "Hans"});
            store.Insert(table, id3, documentAsByteArray, new {Field = "Bjarne"});

            QueryStats stats;
            var rows = store.Query(table, out stats, where: "Field != @name", parameters: new { name = "Bjarne" }).ToList();

            rows.Count().ShouldBe(2);
            var first = rows.Single(x => (Guid) x[table.IdColumn] == id1);
            first[table.EtagColumn].ShouldBe(etag1);
            first[table.DocumentColumn].ShouldBe(documentAsByteArray);
            first[table["Field"]].ShouldBe("Asger");

            var second = rows.Single(x => (Guid)x[table.IdColumn] == id2);
            second[table.IdColumn].ShouldBe(id2);
            second[table.EtagColumn].ShouldBe(etag2);
            second[table.DocumentColumn].ShouldBe(documentAsByteArray);
            second[table["Field"]].ShouldBe("Hans");
        }

        [Fact]
        public void CanQueryAndReturnAnonymousProjections()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();

            store.Insert(table, id, documentAsByteArray, new { Field = "Asger" });

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
        public void CanQueryDynamicTable()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, id1, documentAsByteArray, new { Field = "Asger", StringProp = "A" });
            store.Insert(table, id2, documentAsByteArray, new { Field = "Hans", StringProp = "B" });

            QueryStats stats;
            var rows = store.Query(new Table("Entities"), out stats, where: "Field = @name", parameters: new { name = "Asger" }).ToList();

            rows.Count().ShouldBe(1);
            var row = rows.Single();
            row[table["Field"]].ShouldBe("Asger");
            row[table["StringProp"]].ShouldBe("A");
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
            store1.Migration.InitializeDatabase();

            var store2 = DocumentStore.ForTesting("data source=.;Integrated Security=True");
            store2.ForDocument<Case>().Projection(x => x.By);

            Should.NotThrow(store2.Migration.InitializeDatabase);
        }

        [Fact]
        public void CanSplitLargeCommandBatches()
        {
            var table = store.Configuration.GetTableFor<Entity>();

            var commands = new List<DatabaseCommand>();
            for (int i = 0; i < 2100/4+1; i++)
            {
                commands.Add(new InsertCommand(table, Guid.NewGuid(), documentAsByteArray, new {Field = "A"}));
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
        public void CanStoreAndQueryOnNull()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table, id, new byte[0], new {StringProp = (string)null});

            QueryStats stats;
            var result = store.Query(table, out stats, where: "(@Value IS NULL AND StringProp IS NULL) OR StringProp = @Value", parameters: new { Value = (string)null });
            result.Count().ShouldBe(1);
        }

        [Fact]
        public void CanStoreAndQueryDateTimeProjection()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table, id, new byte[0], new {DateTimeProp = new DateTime(2001, 12, 24, 1, 1, 1)});

            QueryStats stats;
            var result = store.Query(table, out stats, where: "DateTimeProp = @dtp", parameters: new { dtp = new DateTime(2001, 12, 24, 1, 1, 1) });
            result.First()[table["DateTimeProp"]].ShouldBe(new DateTime(2001, 12, 24, 1, 1, 1));
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
            stats.TotalResults.ShouldBe(10);
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
            stats.TotalResults.ShouldBe(10);
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
            stats.TotalResults.ShouldBe(10);
        }


        [Fact]
        public void CanGetTotalRowsWhenOrderingByPropertyWithSameValue()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = 10 });
            store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = 10 });
            store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = 10 });
            store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = 10 });
            store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = 11 });
            store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = 11 });

            QueryStats stats;
            var result = store.Query(table, out stats, @orderby: "Property", skip: 1).ToList();
            result.Count.ShouldBe(5);
            stats.TotalResults.ShouldBe(6);
        }


        [Fact]
        public void CanGetTotalRows()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (int i = 0; i < 10; i++)
                store.Insert(table, Guid.NewGuid(), new byte[0], new { Property = i });

            QueryStats stats;
            var result = store.Query(table, out stats, where: "Property >= 5", skip: 1).ToList();

            result.Count.ShouldBe(4);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public void CanGetTotalRowsWithNoResults()
        {
            var table = store.Configuration.GetTableFor<Entity>();

            QueryStats stats;
            var result = store.Query(table, out stats).ToList();

            result.Count.ShouldBe(0);
            stats.TotalResults.ShouldBe(0);
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
        public void FailsIfEntityTypeIsUnknown()
        {
            Should.Throw<TableNotFoundException>(() => store.Configuration.GetTableFor<int>());
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
            public string NonProjectedField { get; set; }
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

        public class ProjectionWithNonProjectedField
        {
            public string NonProjectedField { get; set; }
        }

        public enum SomeFreakingEnum
        {
            One,
            Two
        }
    }
}