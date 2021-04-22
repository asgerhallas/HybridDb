using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using HybridDb.Commands;
using HybridDb.Config;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class DocumentStoreTests : HybridDbTests
    {
        //readonly byte[] documentAsByteArray = { (byte)'a', (byte)'s', (byte)'g', (byte)'e', (byte)'r' };
        readonly string documentAsByteArray = "asger";

        public DocumentStoreTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void CanInsert()
        {
            Document<Entity>().With(x => x.Field);

            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>().Table;
            store.Insert(table, id, new { Field = "Asger", Document = documentAsByteArray });

            var row = store.Query(table, out _).Single();

            //var row = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((string)row["Id"]).ShouldBe(id);
            ((Guid)row["Etag"]).ShouldNotBe(Guid.Empty);
            ((string)row["Document"]).ShouldBe("asger");
            ((string)row["Field"]).ShouldBe("Asger");
        }

        [Fact]
        public void CanUpdate()
        {
            Document<Entity>().With(x => x.Field);

            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>().Table;
            var etag = store.Insert(table, id, new { Field = "Asger" });

            store.Update(table, id, etag, new { Field = "Lars" });

            var row = store.Query(table, out _).Single();
            ((Guid)row["Etag"]).ShouldNotBe(etag);
            ((string)row["Field"]).ShouldBe("Lars");
        }

        [Fact]
        public void CanUpdatePessimistically()
        {
            Document<Entity>().With(x => x.Field);

            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new { Field = "Asger", Document = "asger" });

            Should.NotThrow(() => store.Update(table.Table, id, null, new { Field = "Lars" }));
        }

        [Fact]
        public void UpdateFailsWhenEtagNotMatch()
        {
            Document<Entity>().With(x => x.Field);

            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            Should.Throw<ConcurrencyException>(() => store.Update(table.Table, id, Guid.NewGuid(), new { Field = "Lars" }));
        }

        [Fact]
        public void UpdateFailsWhenIdNotMatchAkaObjectDeleted()
        {
            Document<Entity>().With(x => x.Field);

            var id = NewId();
            var etag = Guid.NewGuid();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            Should.Throw<ConcurrencyException>(() => store.Update(table.Table, NewId(), etag, new { Field = "Lars" }));
        }

        [Fact]
        public void CanGet()
        {
            Document<Entity>().With(x => x.Field).With(x => x.Complex.ToString());

            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = store.Insert(table.Table, id, new { Field = "Asger", ComplexToString = "AB", Document = documentAsByteArray });

            var row = store.Get(table.Table, id);
            row[DocumentTable.IdColumn].ShouldBe(id);
            row[DocumentTable.EtagColumn].ShouldBe(etag);
            row[DocumentTable.DocumentColumn].ShouldBe(documentAsByteArray);
            row[table.Table["Field"]].ShouldBe("Asger");
            row[table.Table["ComplexToString"]].ShouldBe("AB");
        }

        [Fact]
        public void CanGetDynamically()
        {
            Document<Entity>().With(x => x.Field);

            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            var row = store.Get(new DocumentTable("Entities"), id);
            row[DocumentTable.IdColumn].ShouldBe(id);
            row[DocumentTable.EtagColumn].ShouldBe(etag);
            row[DocumentTable.DocumentColumn].ShouldBe(documentAsByteArray);
            row[table.Table["Field"]].ShouldBe("Asger");
        }

        [Fact]
        public void CanQueryProjectToNestedProperty()
        {
            Document<Entity>().With(x => x.TheChild.NestedDouble);

            var id1 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id1, new { TheChildNestedDouble = 9.8d });

            QueryStats stats;
            var rows = store.Query<ProjectionWithNestedProperty>(table.Table, out stats).ToList();

            rows.Single().Data.TheChildNestedDouble.ShouldBe(9.8d);
        }

        [Fact]
        public void CanQueryAndReturnFullDocuments()
        {
            Document<Entity>().With(x => x.Field);

            var id1 = NewId();
            var id2 = NewId();
            var id3 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag1 = store.Insert(table.Table, id1, new { Field = "Asger", Document = documentAsByteArray });
            var etag2 = store.Insert(table.Table, id2, new { Field = "Hans", Document = documentAsByteArray });
            store.Insert(table.Table, id3, new { Field = "Bjarne", Document = documentAsByteArray });

            QueryStats stats;
            var rows = store.Query(table.Table, out stats, where: "Field != @name", parameters: new { name = "Bjarne" }).ToList();

            rows.Count().ShouldBe(2);
            var first = rows.Single(x => (string)x[DocumentTable.IdColumn] == id1);
            first[DocumentTable.EtagColumn].ShouldBe(etag1);
            first[DocumentTable.DocumentColumn].ShouldBe(documentAsByteArray);
            first[table.Table["Field"]].ShouldBe("Asger");

            var second = rows.Single(x => (string)x[DocumentTable.IdColumn] == id2);
            second[DocumentTable.IdColumn].ShouldBe(id2);
            second[DocumentTable.EtagColumn].ShouldBe(etag2);
            second[DocumentTable.DocumentColumn].ShouldBe(documentAsByteArray);
            second[table.Table["Field"]].ShouldBe("Hans");
        }

        [Fact]
        public void CanQueryAndReturnAnonymousProjections()
        {
            Document<Entity>().With(x => x.Field);

            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();

            store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            var t = new { Field = "" };

            IEnumerable<dynamic> Query<U>(U prototype) => 
                store.Query<U>(table.Table, out _, false, null, "Field = @name", null, "", false, new {name = "Asger"});
            
            var rows = Query(t).ToList();

            rows.Count.ShouldBe(1);
            Assert.Equal("Asger", rows.Single().Data.Field);
        }

        [Fact]
        public void CanQueryAndReturnValueProjections()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();

            store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            QueryStats stats;
            var rows = store.Query<string>(table.Table, out stats, select: "Field").Select(x => x.Data).ToList();

            Assert.Equal("Asger", rows.Single());
        }

        [Fact]
        public void CanQueryDynamicTable()
        {
            Document<Entity>().With(x => x.Field).With(x => x.Property);
            
            var id1 = NewId();
            var id2 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id1, new { Field = "Asger", Property = "A", Document = documentAsByteArray });
            store.Insert(table.Table, id2, new { Field = "Hans", Property = "B", Document = documentAsByteArray });

            QueryStats stats;
            var rows = store.Query(new DocumentTable("Entities"), out stats, where: "Field = @name", parameters: new { name = "Asger" }).ToList();

            rows.Count().ShouldBe(1);
            var row = rows.Single();
            row[table.Table["Field"]].ShouldBe("Asger");
            row[table.Table["Property"]].ShouldBe("A");
        }

        [Fact]
        public void CanDelete()
        {
            Document<Entity>();
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = store.Insert(table.Table, id, new { });

            store.Delete(table.Table, id, etag);

            store.Query<object>(table.Table, out _).Count().ShouldBe(0);
        }

        [Fact]
        public void CanDeletePessimistically()
        {
            Document<Entity>();
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new { });

            Should.NotThrow(() => store.Delete(table.Table, id, null));
        }

        [Fact]
        public void DeleteFailsWhenEtagNotMatch()
        {
            Document<Entity>();
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new { });

            Should.Throw<ConcurrencyException>(() => store.Delete(table.Table, id, Guid.NewGuid()));
        }

        [Fact]
        public void DeleteFailsWhenIdNotMatchAkaDocumentAlreadyDeleted()
        {
            Document<Entity>();
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = store.Insert(table.Table, id, new { });

            Should.Throw<ConcurrencyException>(() => store.Delete(table.Table, NewId(), etag));
        }

        [Fact]
        public void CanBatchCommandsAndGetEtag()
        {
            Document<Entity>().With(x => x.Field);
            
            var id1 = NewId();
            var id2 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var etag = store.Transactionally(tx =>
            {
                tx.Execute(new InsertCommand(table, id1, new {Field = "A"}));
                return tx.Execute(new InsertCommand(table, id2, new {Field = "B"}));
            });

            var rows = store.Query<Guid>(table, out _, select: "Etag", orderby: "Field").ToList();

            rows.Count.ShouldBe(2);
            rows[0].Data.ShouldBe(etag);
            rows[1].Data.ShouldBe(etag);
        }

        [Fact]
        public void BatchesAreTransactional()
        {
            Document<Entity>().With(x => x.Field);
            
            var id1 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>().Table;
            var etagThatMakesItFail = Guid.NewGuid();
            try
            {
                store.Transactionally(tx =>
                {
                    tx.Execute(new InsertCommand(table, id1, new { Field = "A" }));
                    return tx.Execute(new UpdateCommand(table, id1, etagThatMakesItFail, new { Field = "B" }));
                });
            }
            catch (ConcurrencyException)
            {
                // ignore the exception and ensure that nothing was inserted
            }

            store.Query(table, out _).Count().ShouldBe(0);
        }

        [Fact]
        public void CanStoreAndQueryEnumProjection()
        {
            Document<Entity>().With(x => x.EnumProp);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            store.Insert(table.Table, id, new { EnumProp = SomeFreakingEnum.Two });

            var result = store.Get(table.Table, id);
            result[table.Table["EnumProp"]].ShouldBe(SomeFreakingEnum.Two.ToString());
        }

        [Fact]
        public void CanStoreAndQueryEnumProjectionToNetType()
        {
            Document<Entity>().With(x => x.EnumProp);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            store.Insert(table.Table, id, new { EnumProp = SomeFreakingEnum.Two });

            QueryStats stats;
            var result = store.Query<ProjectionWithEnum>(table.Table, out stats).Single();
            result.Data.EnumProp.ShouldBe(SomeFreakingEnum.Two);
        }

        [Fact]
        public void CanStoreAndQueryStringProjection()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            store.Insert(table.Table, id, new { Property = "Hest" });

            var result = store.Get(table.Table, id);
            result[table.Table["Property"]].ShouldBe("Hest");
        }

        [Fact]
        public void CanStoreAndQueryOnNull()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            store.Insert(table.Table, id, new { Property = (string)null });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, where: "(@Value IS NULL AND Property IS NULL) OR Property = @Value", parameters: new { Value = (string)null });
            result.Count().ShouldBe(1);
        }

        [Fact]
        public void CanStoreAndQueryDateTimeProjection()
        {
            Document<Entity>().With(x => x.DateTimeProp);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            store.Insert(table.Table, id, new { DateTimeProp = new DateTime(2001, 12, 24, 1, 1, 1) });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, where: "DateTimeProp = @dtp", parameters: new { dtp = new DateTime(2001, 12, 24, 1, 1, 1) });
            result.First()[table.Table["DateTimeProp"]].ShouldBe(new DateTime(2001, 12, 24, 1, 1, 1));
        }

        [Fact]
        public void CanPage()
        {
            Document<Entity>().With(x => x.Number);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, NewId(), new { Number = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, window: new SkipTake(2, 5), orderby: "Number").ToList();

            result.Count.ShouldBe(5);
            var props = result.Select(x => x[table.Table["Number"]]).ToList();
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
            Document<Entity>().With(x => x.Number);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, NewId(), new { Number = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, window: new SkipTake(0, 2), orderby: "Number").ToList();

            result.Count.ShouldBe(2);
            var props = result.Select(x => x[table.Table["Number"]]).ToList();
            props.ShouldContain(0);
            props.ShouldContain(1);
            stats.TotalResults.ShouldBe(10);
        }

        [Fact]
        public void CanSkip()
        {
            Document<Entity>().With(x => x.Number);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, NewId(), new { Number = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, window: new SkipTake(7, 0), orderby: "Number").ToList();

            result.Count.ShouldBe(3);
            var props = result.Select(x => x[table.Table["Number"]]).ToList();
            props.ShouldContain(7);
            props.ShouldContain(8);
            props.ShouldContain(9);
            stats.TotalResults.ShouldBe(10);
        }

        [Fact]
        public void CanQueryWithoutWhere()
        {
            Document<Entity>();
            
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, NewId(), new { });

            var result = store.Query(table.Table, out _).ToList();

            result.Count.ShouldBe(1);
        }

        [Fact]
        public void CanGetStats()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, NewId(), new { Property = i });

            store.Query(table.Table, out var stats, where: "Property >= 5");

            stats.RetrievedResults.ShouldBe(5);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public void CanGetStatsWhenSkipping()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, NewId(), new { Property = i });

            store.Query(table.Table, out var stats, where: "Property >= 5", window: new SkipTake(1, 0));

            stats.RetrievedResults.ShouldBe(4);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public void CanGetStatsWithNoResults()
        {
            Document<Entity>();
            
            var table = store.Configuration.GetDesignFor<Entity>();

            store.Query(table.Table, out var stats);

            stats.RetrievedResults.ShouldBe(0);
            stats.TotalResults.ShouldBe(0);
        }

        [Fact]
        public void CanGetStatsWhenOrderingByPropertyWithSameValue()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, NewId(), new { Property = 10 });
            store.Insert(table.Table, NewId(), new { Property = 10 });
            store.Insert(table.Table, NewId(), new { Property = 10 });
            store.Insert(table.Table, NewId(), new { Property = 10 });
            store.Insert(table.Table, NewId(), new { Property = 11 });
            store.Insert(table.Table, NewId(), new { Property = 11 });

            store.Query(table.Table, out var stats, @orderby: "Property", window: new SkipTake(1, 0));
            
            stats.RetrievedResults.ShouldBe(5);
            stats.TotalResults.ShouldBe(6);
        }

        [Fact]
        public void CanGetStatsWhenSkippingAllOrMore()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, NewId(), new { Property = i });

            QueryStats stats;
            store.Query(table.Table, out stats, where: "Property >= 5", window: new SkipTake(10, 0));

            stats.RetrievedResults.ShouldBe(0);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public void CanGetStatsWhenTaking()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, NewId(), new { Property = i });

            QueryStats stats;
            store.Query(table.Table, out stats, where: "Property >= 5", window: new SkipTake(0, 2));

            stats.RetrievedResults.ShouldBe(2);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public void CanGetStatsWhenTakingAllOrMore()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, NewId(), new { Property = i });

            QueryStats stats;
            store.Query(table.Table, out stats, where: "Property >= 5", window: new SkipTake(0, 20));

            stats.RetrievedResults.ShouldBe(5);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public void CanOrderBy()
        {
            Document<Entity>().With(x => x.Field);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 5; i > 0; i--)
                store.Insert(table.Table, NewId(), new { Field = i });

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
        public void CanOrderByIdAndSelectOtherField()
        {
            Document<Entity>().With(x => x.Field);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 5; i > 0; i--)
                store.Insert(table.Table, i.ToString(), new { Field = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, select: "Field", orderby: "Id").ToList();

            var props = result.Select(x => x[table.Table["Field"]]).ToList();
            props[0].ShouldBe("1");
            props[1].ShouldBe("2");
            props[2].ShouldBe("3");
            props[3].ShouldBe("4");
            props[4].ShouldBe("5");
        }

        [Fact]
        public void CanOrderByIdAndSelectOtherFieldWindowed()
        {
            Document<Entity>().With(x => x.Field);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 5; i > 0; i--)
                store.Insert(table.Table, i.ToString(), new { Field = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, select: "Field", orderby: "Id", window: new SkipTake(1, 1)).Single();

            result[table.Table["Field"]].ShouldBe("2");
        }

        [Fact]
        public void CanOrderByDescWhileSkippingAndTaking()
        {
            Document<Entity>().With(x => x.Field);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 5; i > 0; i--)
                store.Insert(table.Table, NewId(), new { Field = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, window: new SkipTake(2, 2), orderby: "Field desc").ToList();

            var props = result.Select(x => x[table.Table["Field"]]).ToList();
            props[0].ShouldBe("3");
            props[1].ShouldBe("2");
        }

        [Fact]
        public void WillEnlistCommandsInAmbientTransactions()
        {
            Document<Entity>();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            using (new TransactionScope())
            {
                store.Insert(table, NewId(), new { });
                store.Insert(table, NewId(), new { });
            }

            store.Query(table, out _).Count().ShouldBe(0);
        }

        [Fact]
        public void CanUseTempDb()
        {
            var prefix = "myprefix";

            UseGlobalTempTables();

            void configurator(Configuration x)
            {
                x.UseConnectionString(connectionString);
                x.UseTableNamePrefix(prefix);
                x.Document<Case>();
            }

            using (var globalStore1 = DocumentStore.ForTesting(TableMode.GlobalTempTables, configurator))
            {
                var id = NewId();
                globalStore1.Insert(globalStore1.Configuration.GetDesignFor<Case>().Table, id, new { });

                using (var globalStore2 = DocumentStore.ForTesting(TableMode.GlobalTempTables, configurator))
                {
                    globalStore2.Get(globalStore2.Configuration.GetDesignFor<Case>().Table, id).ShouldNotBe(null);
                }

                globalStore1.Get(globalStore1.Configuration.GetDesignFor<Case>().Table, id).ShouldNotBe(null);
            }
        }

        [Fact]
        public void UtilityColsAreRemovedFromQueryResults()
        {
            Document<Entity>();

            var table = new DocumentTable("Entities");
            store.Insert(table, NewId(), new { Version = 1 });

            var result1 = store.Query(table, out _, window: new SkipTake(0, 2)).Single();
            result1.ContainsKey(new Column("RowNumber", typeof(int))).ShouldBe(false);
            result1.ContainsKey(new Column("TotalResults", typeof(int))).ShouldBe(false);

            var result2 = store.Query<object>(table, out _, window: new SkipTake(0, 2)).Single();
            ((IDictionary<string, object>)result2.Data).ContainsKey("RowNumber").ShouldBe(false);
            ((IDictionary<string, object>)result2.Data).ContainsKey("TotalResults").ShouldBe(false);
        }

        [Fact]
        public void CanQueryWithConcatenation()
        {
            Document<Entity>().With(x => x.Property);
            Document<OtherEntityWithSomeSimilarities>().With(x => x.Property);
        }

        [Fact]
        public void CanExecuteOnMultipleThreads()
        {
            UseGlobalTempTables();

            UseTableNamePrefix(Guid.NewGuid().ToString());
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            Parallel.For(0, 10, x =>
            {
                var table = new DocumentTable("Entities");
                store.Insert(table, NewId(), new { Property = "Asger", Version = 1 });
            });
        }

        [Fact]
        public void Bug_InsertLargeDocument()
        {
            Document<object>();

            store.Insert(store.Configuration.GetDesignFor<object>().Table, "a", new {Document = new string('*', 10_000_000)});
        }

        [Theory]
        [InlineData(0, new[] { 0, 1, 2, 3, 4 })]
        [InlineData(3, new[] { 0, 1, 2, 3, 4 })]
        [InlineData(4, new[] { 0, 1, 2, 3, 4 })]
        [InlineData(5, new[] { 5, 6, 7, 8, 9 })]
        [InlineData(7, new[] { 5, 6, 7, 8, 9 })]
        [InlineData(9, new[] { 5, 6, 7, 8, 9 })]
        [InlineData(10, new[] { 10, 11, 12, 13, 14 })]
        [InlineData(15, new[] { 15, 16 })]
        [InlineData(16, new[] { 15, 16 })]
        public void CanSkipToId(int index, int[] expected)
        {
            Document<Entity>().With(x => x.Number);

            var ids = new List<string>();
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 17; i++)
            {
                var id = Guid.NewGuid().ToString();
                ids.Add(id);
                store.Insert(table.Table, id, new { Number = i });
            }

            QueryStats stats;
            var result = store.Query(table.Table, out stats, window: new SkipToId(ids[index], 5), orderby: "Number").ToList();

            result.Select(x => (int)x[table.Table["Number"]])
                .ShouldBe(expected);

            stats.TotalResults.ShouldBe(17);
            stats.RetrievedResults.ShouldBe(expected.Length);
            stats.FirstRowNumberOfWindow.ShouldBe(expected[0]);
        }

        [Theory]
        [InlineData(0, 0, new[] { 0, 2, 4, 6, 8 })]
        [InlineData(2, 0, new[] { 0, 2, 4, 6, 8 })]
        [InlineData(3, 0, new[] { 0, 2, 4, 6, 8 })]
        [InlineData(10, 5, new[] { 10, 12, 14, 16 })]
        [InlineData(11, 0, new[] { 0, 2, 4, 6, 8 })]
        public void CanSkipToId_Where(int index, int firstRowOfWindow, int[] expected)
        {
            Document<Entity>().With(x => x.Number);

            var ids = new List<string>();
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 17; i++)
            {
                var id = Guid.NewGuid().ToString();
                ids.Add(id);
                store.Insert(table.Table, id, new { Number = i });
            }

            QueryStats stats;
            var result = store.Query(table.Table, out stats, where: "Number % 2 = 0", window: new SkipToId(ids[index], 5), orderby: "Number").ToList();

            result.Select(x => (int)x[table.Table["Number"]])
                .ShouldBe(expected);

            stats.TotalResults.ShouldBe(9);
            stats.RetrievedResults.ShouldBe(expected.Length);
            stats.FirstRowNumberOfWindow.ShouldBe(firstRowOfWindow);
        }

        [Fact]
        public void CanSkipToId_Where_NoResults()
        {
            Document<Entity>().With(x => x.Number);

            var ids = new List<string>();
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 17; i++)
            {
                var id = Guid.NewGuid().ToString();
                ids.Add(id);
                store.Insert(table.Table, id, new { Number = i });
            }

            QueryStats stats;
            var result = store.Query(table.Table, out stats, where: "Number = -1", window: new SkipToId(ids[0], 5), orderby: "Number").ToList();

            result.Select(x => (int)x[table.Table["Number"]])
                .ShouldBeEmpty();

            stats.TotalResults.ShouldBe(0);
            stats.RetrievedResults.ShouldBe(0);
            stats.FirstRowNumberOfWindow.ShouldBe(0);
        }

        [Fact]
        public void CanJoin()
        {
            Document<Entity>().With(x => x.Number);
            Document<OtherEntity>().With(x => x.Number);

            using (var s1 = store.OpenSession())
            {
                s1.Store(new Entity
                {
                    Id = "a",
                    Number = 1
                });

                s1.Store(new Entity
                {
                    Id = "b",
                    Number = 2
                });

                s1.Store(new OtherEntity
                {
                    Id = "c",
                    Number = 2,
                    
                });

                s1.SaveChanges();
            }
            
            var entities = store.Configuration.GetDesignFor<Entity>();
            var otherEntities = store.Configuration.GetDesignFor<OtherEntity>();

            var result = store.Query<IDictionary<string, object>>(
                entities.Table, 
                $"inner join {store.Database.FormatTableNameAndEscape(otherEntities.Table.Name)} on {store.Database.FormatTableNameAndEscape(entities.Table.Name)}.Number = {store.Database.FormatTableNameAndEscape(otherEntities.Table.Name)}.Number", 
                out var stats,
                select: $"{store.Database.FormatTableNameAndEscape(entities.Table.Name)}.Id, {store.Database.FormatTableNameAndEscape(otherEntities.Table.Name)}.Id as OtherId"
            ).ToList();

            result.Count.ShouldBe(1);
            result[0].Data.Get<string>("Id").ShouldBe("b");
            result[0].Data.Get<string>("OtherId").ShouldBe("c");
        }


        public class Case
        {
            public Guid Id { get; private set; }
            public string By { get; set; }
        }

        public class OtherEntityWithSomeSimilarities
        {
            public Guid Id { get; set; }
            public int Property { get; set; }
            public string StringProp { get; set; }
        }

        public class ProjectionWithNestedProperty
        {
            public double TheChildNestedDouble { get; set; }
        }

        public class ProjectionWithEnum
        {
            public SomeFreakingEnum EnumProp { get; set; }
        }
    }
}