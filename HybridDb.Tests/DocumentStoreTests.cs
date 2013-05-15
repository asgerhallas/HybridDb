using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Transactions;
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
            store = DocumentStore.ForTestingWithTempTables("data source=.;Integrated Security=True");
            store.DocumentsFor<Entity>()
                 .Project(x => x.Field)
                 .Project(x => x.Property)
                 .Project(x => x.TheChild.NestedProperty)
                 .Project(x => x.StringProp)
                 .Project(x => x.DateTimeProp)
                 .Project(x => x.EnumProp)
                 .Project("Children", x => x.Children.Select(y => y.NestedString));
            store.InitializeDatabase();

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
            store.Insert(table.Table, id, documentAsByteArray, new {Field = "Asger"});

            var row = store.RawQuery("select * from #Entities").Single();
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
                         documentAsByteArray, new {Field = "Asger"});

            var row = store.RawQuery("select * from #Entities").Single();
            ((Guid) row.Id).ShouldBe(id);
            ((Guid) row.Etag).ShouldNotBe(Guid.Empty);
            Encoding.ASCII.GetString((byte[]) row.Document).ShouldBe("asger");
            ((string) row.Field).ShouldBe("Asger");
        }

        [Fact]
        public void CanInsertNullsDynamically()
        {
            store.Insert(new Table("Entities"),
                         Guid.NewGuid(),
                         documentAsByteArray,
                         new Dictionary<string, object> {{"Field", null}});

            var row = store.RawQuery("select * from #Entities").Single();
            ((string) row.Field).ShouldBe(null);
        }

        [Fact]
        public void CanInsertCollectionProjections()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, id, documentAsByteArray,
                         new
                         {
                             Children = new[]
                             {
                                 new {NestedString = "A"},
                                 new {NestedString = "B"}
                             }
                         });

            var mainrow = store.RawQuery("select * from #Entities").Single();
            ((Guid)mainrow.Id).ShouldBe(id);

            var utilrows = store.RawQuery("select * from #Entities_Children").ToList();
            utilrows.Count.ShouldBe(2);
            
            var utilrow = utilrows.First();
            ((Guid)utilrow.DocumentId).ShouldBe(id);
            ((string)utilrow.NestedString).ShouldBe("A");
        }

        [Fact]
        public void CanUpdate()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table.Table, id, documentAsByteArray, new {Field = "Asger"});

            store.Update(table.Table, id, etag, new byte[] {}, new {Field = "Lars"});

            var row = store.RawQuery("select * from #Entities").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe("Lars");
        }

        [Fact]
        public void CanUpdateDynamically()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table.Table, id, documentAsByteArray, new {Field = "Asger"});

            store.Update(new Table("Entities"), id, etag, new byte[] {}, new Dictionary<string, object> {{"Field", null}, {"StringProp", "Lars"}});

            var row = store.RawQuery("select * from #Entities").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe(null);
            ((string) row.StringProp).ShouldBe("Lars");
        }

        [Fact]
        public void CanUpdatePessimistically()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, id, new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'}, new {Field = "Asger"});

            Should.NotThrow(() => store.Update(table.Table, id, Guid.NewGuid(), new byte[] {}, new {Field = "Lars"}, lastWriteWins: true));
        }

        [Fact]
        public void UpdateFailsWhenEtagNotMatch()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, id, documentAsByteArray, new {Field = "Asger"});

            Should.Throw<ConcurrencyException>(() => store.Update(table.Table, id, Guid.NewGuid(), new byte[] {}, new {Field = "Lars"}));
        }

        [Fact]
        public void UpdateFailsWhenIdNotMatchAkaObjectDeleted()
        {
            var id = Guid.NewGuid();
            var etag = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, id, documentAsByteArray, new {Field = "Asger"});

            Should.Throw<ConcurrencyException>(() => store.Update(table.Table, Guid.NewGuid(), etag, new byte[] {}, new {Field = "Lars"}));
        }

        [Fact]
        public void CanGet()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table.Table, id, documentAsByteArray, new {Field = "Asger"});

            var row = store.Get(table.Table, id);
            row[table.Table.IdColumn].ShouldBe(id);
            row[table.Table.EtagColumn].ShouldBe(etag);
            row[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            row[table.Table["Field"]].ShouldBe("Asger");
        }

        [Fact]
        public void CanGetDynamically()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table.Table, id, documentAsByteArray, new { Field = "Asger" });

            var row = store.Get(new Table("Entities"), id);
            row[table.Table.IdColumn].ShouldBe(id);
            row[table.Table.EtagColumn].ShouldBe(etag);
            row[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            row[table.Table["Field"]].ShouldBe("Asger");
        }

        [Fact]
        public void CanQueryProjectToNestedProperty()
        {
            var id1 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, id1, new byte[0], new { TheChildNestedProperty = 9.8d });

            QueryStats stats;
            var rows = store.Query<ProjectionWithNestedProperty>(table.Table, out stats).ToList();

            rows.Single().TheChildNestedProperty.ShouldBe(9.8d);
        }

        [Fact]
        public void CanQueryAndReturnFullDocuments()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag1 = store.Insert(table.Table, id1, documentAsByteArray, new { Field = "Asger" });
            var etag2 = store.Insert(table.Table, id2, documentAsByteArray, new { Field = "Hans" });
            store.Insert(table.Table, id3, documentAsByteArray, new { Field = "Bjarne" });

            QueryStats stats;
            var rows = store.Query(table.Table, out stats, where: "Field != @name", parameters: new { name = "Bjarne" }).ToList();

            rows.Count().ShouldBe(2);
            var first = rows.Single(x => (Guid)x[table.Table.IdColumn] == id1);
            first[table.Table.EtagColumn].ShouldBe(etag1);
            first[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            first[table.Table["Field"]].ShouldBe("Asger");

            var second = rows.Single(x => (Guid)x[table.Table.IdColumn] == id2);
            second[table.Table.IdColumn].ShouldBe(id2);
            second[table.Table.EtagColumn].ShouldBe(etag2);
            second[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            second[table.Table["Field"]].ShouldBe("Hans");
        }

        [Fact]
        public void CanQueryAndReturnAnonymousProjections()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();

            store.Insert(table.Table, id, documentAsByteArray, new { Field = "Asger" });

            var t = new {Field = ""};

            QueryStats stats = null;
            var methodInfo = (from method in store.GetType().GetMethods()
                              where method.Name == "Query" && method.IsGenericMethod
                              select method).Single().MakeGenericMethod(t.GetType());

            var rows = (IEnumerable<dynamic>) methodInfo.Invoke(store,
                                                                new object[] {table, stats, null, "Field = @name", 0, 0, "", new {name = "Asger"}});

            rows.Count().ShouldBe(1);
            Assert.Equal("Asger", rows.Single().Field);
        }

        [Fact]
        public void CanQueryDynamicTable()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, id1, documentAsByteArray, new { Field = "Asger", StringProp = "A" });
            store.Insert(table.Table, id2, documentAsByteArray, new { Field = "Hans", StringProp = "B" });

            QueryStats stats;
            var rows = store.Query(new Table("Entities"), out stats, where: "Field = @name", parameters: new {name = "Asger"}).ToList();

            rows.Count().ShouldBe(1);
            var row = rows.Single();
            row[table.Table["Field"]].ShouldBe("Asger");
            row[table.Table["StringProp"]].ShouldBe("A");
        }

        [Fact]
        public void CanDelete()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table.Table, id, new byte[0], new { });

            store.Delete(table.Table, id, etag);

            store.RawQuery("select * from #Entities").Count().ShouldBe(0);
        }

        [Fact]
        public void CanDeletePessimistically()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, id, new byte[0], new { });

            Should.NotThrow(() => store.Delete(table.Table, id, Guid.NewGuid(), lastWriteWins: true));
        }

        [Fact]
        public void DeleteFailsWhenEtagNotMatch()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, id, new byte[0], new { });

            Should.Throw<ConcurrencyException>(() => store.Delete(table.Table, id, Guid.NewGuid()));
        }

        [Fact]
        public void DeleteFailsWhenIdNotMatchAkaDocumentAlreadyDeleted()
        {
            var id = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Insert(table.Table, id, new byte[0], new { });

            Should.Throw<ConcurrencyException>(() => store.Delete(table.Table, Guid.NewGuid(), etag));
        }

        [Fact]
        public void CanBatchCommandsAndGetEtag()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var table = store.Configuration.GetTableFor<Entity>();
            var etag = store.Execute(new InsertCommand(table.Table, id1, new byte[0], new { Field = "A" }),
                                     new InsertCommand(table.Table, id2, new byte[0], new { Field = "B" }));

            var rows = store.RawQuery<Guid>("select Etag from #Entities order by Field").ToList();
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
                store.Execute(new InsertCommand(table.Table, id1, new byte[0], new { Field = "A" }),
                              new UpdateCommand(table.Table, id1, etagThatMakesItFail, new byte[0], new { Field = "B" }, false));
            }
            catch (ConcurrencyException)
            {
                // ignore the exception and ensure that nothing was inserted
            }

            store.RawQuery("select * from #Entities").Count().ShouldBe(0);
        }


        [Fact]
        public void WillNotCreateSchemaIfItAlreadyExists()
        {
            var store1 = DocumentStore.ForTestingWithTempTables("data source=.;Integrated Security=True");
            store1.DocumentsFor<Case>().Project(x => x.By);
            store1.InitializeDatabase();

            var store2 = DocumentStore.ForTestingWithTempTables("data source=.;Integrated Security=True");
            store2.DocumentsFor<Case>().Project(x => x.By);

            Should.NotThrow(() => store2.InitializeDatabase());
        }

        [Fact]
        public void CanSplitLargeCommandBatches()
        {
            var table = store.Configuration.GetTableFor<Entity>();

            var commands = new List<DatabaseCommand>();
            for (var i = 0; i < 2100/4 + 1; i++)
            {
                commands.Add(new InsertCommand(table.Table, Guid.NewGuid(), documentAsByteArray, new { Field = "A" }));
            }

            store.Execute(commands.ToArray());
            store.NumberOfRequests.ShouldBe(2);
        }

        [Fact]
        public void CanStoreAndQueryEnumProjection()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table.Table, id, new byte[0], new { EnumProp = SomeFreakingEnum.Two });

            var result = store.Get(table.Table, id);
            result[table.Table["EnumProp"]].ShouldBe(SomeFreakingEnum.Two.ToString());
        }

        [Fact]
        public void CanStoreAndQueryEnumProjectionToNetType()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table.Table, id, new byte[0], new { EnumProp = SomeFreakingEnum.Two });

            QueryStats stats;
            var result = store.Query<ProjectionWithEnum>(table.Table, out stats).Single();
            result.EnumProp.ShouldBe(SomeFreakingEnum.Two);
        }

        [Fact]
        public void CanStoreAndQueryStringProjection()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table.Table, id, new byte[0], new { StringProp = "Hest" });

            var result = store.Get(table.Table, id);
            result[table.Table["StringProp"]].ShouldBe("Hest");
        }

        [Fact]
        public void CanStoreAndQueryOnNull()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table.Table, id, new byte[0], new { StringProp = (string)null });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, where: "(@Value IS NULL AND StringProp IS NULL) OR StringProp = @Value", parameters: new { Value = (string)null });
            result.Count().ShouldBe(1);
        }

        [Fact]
        public void CanStoreAndQueryDateTimeProjection()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            var id = Guid.NewGuid();
            store.Insert(table.Table, id, new byte[0], new { DateTimeProp = new DateTime(2001, 12, 24, 1, 1, 1) });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, where: "DateTimeProp = @dtp", parameters: new { dtp = new DateTime(2001, 12, 24, 1, 1, 1) });
            result.First()[table.Table["DateTimeProp"]].ShouldBe(new DateTime(2001, 12, 24, 1, 1, 1));
        }

        [Fact]
        public void CanPage()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, skip: 2, take: 5, orderby: "Property").ToList();

            result.Count.ShouldBe(5);
            var props = result.Select(x => x[table.Table["Property"]]).ToList();
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
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, take: 2, orderby: "Property").ToList();

            result.Count.ShouldBe(2);
            var props = result.Select(x => x[table.Table["Property"]]).ToList();
            props.ShouldContain(0);
            props.ShouldContain(1);
            stats.TotalResults.ShouldBe(10);
        }

        [Fact]
        public void CanSkip()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, skip: 7, orderby: "Property").ToList();

            result.Count.ShouldBe(3);
            var props = result.Select(x => x[table.Table["Property"]]).ToList();
            props.ShouldContain(7);
            props.ShouldContain(8);
            props.ShouldContain(9);
            stats.TotalResults.ShouldBe(10);
        }


        [Fact]
        public void CanGetTotalRowsWhenOrderingByPropertyWithSameValue()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = 10 });
            store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = 10 });
            store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = 10 });
            store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = 10 });
            store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = 11 });
            store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = 11 });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, @orderby: "Property", skip: 1).ToList();
            result.Count.ShouldBe(5);
            stats.TotalResults.ShouldBe(6);
        }


        [Fact]
        public void CanGetTotalRows()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Property = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, where: "Property >= 5", skip: 1).ToList();

            result.Count.ShouldBe(4);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public void CanGetTotalRowsWithNoResults()
        {
            var table = store.Configuration.GetTableFor<Entity>();

            QueryStats stats;
            var result = store.Query(table.Table, out stats).ToList();

            result.Count.ShouldBe(0);
            stats.TotalResults.ShouldBe(0);
        }

        [Fact]
        public void CanQueryWithoutWhere()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { });

            QueryStats stats;
            var result = store.Query(table.Table, out stats).ToList();

            result.Count.ShouldBe(1);
        }

        [Fact]
        public void CanOrderBy()
        {
            var table = store.Configuration.GetTableFor<Entity>();
            for (var i = 5; i > 0; i--)
                store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Field = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, orderby: "Field").ToList();

            var props = result.Select(x => x[table.Table["Field"]]).ToList();
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
            for (var i = 5; i > 0; i--)
                store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { Field = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, skip: 2, take: 2, orderby: "Field desc").ToList();

            var props = result.Select(x => x[table.Table["Field"]]).ToList();
            props[0].ShouldBe("3");
            props[1].ShouldBe("2");
        }

        [Fact]
        public void FailsIfEntityTypeIsUnknown()
        {
            Should.Throw<TableNotFoundException>(() => store.Configuration.GetTableFor<int>());
        }

        [Fact]
        public void WillEnlistCommandsInAmbientTransactions()
        {
            using (new TransactionScope())
            {
                var table = store.Configuration.GetTableFor<Entity>();
                store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { });
                store.Insert(table.Table, Guid.NewGuid(), new byte[0], new { });

                // No tx complete here
            }

            store.RawQuery("select * from #Entities").Count().ShouldBe(0);
        }

        [Fact]
        public void CanUseGlobalTempTables()
        {
            using (var globalStore1 = DocumentStore.ForTestingWithGlobalTempTables())
            {
                globalStore1.DocumentsFor<Case>();
                globalStore1.InitializeDatabase();

                var id = Guid.NewGuid();
                globalStore1.Insert(globalStore1.Configuration.GetTableFor<Case>().Table, id, new byte[0], new { });

                using (var globalStore2 = DocumentStore.ForTestingWithGlobalTempTables())
                {
                    globalStore2.DocumentsFor<Case>();
                    var result = globalStore2.Get(globalStore2.Configuration.GetTableFor<Case>().Table, id);

                    result.ShouldNotBe(null);
                }
            }

            var tables = store.RawQuery<string>(string.Format("select OBJECT_ID('##Cases') as Result"));
            tables.First().ShouldBe(null);
        }

        public class Case
        {
            public Guid Id { get; private set; }
            public string By { get; set; }
        }

        public class Entity
        {
            public Entity()
            {
                Children = new List<Child>();
            }

            public string Field;
            public Guid Id { get; private set; }
            public int Property { get; set; }
            public string StringProp { get; set; }
            public string NonProjectedField { get; set; }
            public SomeFreakingEnum EnumProp { get; set; }
            public DateTime DateTimeProp { get; set; }
            public Child TheChild { get; set; }
            public List<Child> Children { get; set; }

            public class Child
            {
                public string NestedString { get; set; }
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