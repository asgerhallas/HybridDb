using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq.Ast;
using HybridDb.Linq2;
using HybridDb.Linq2.Ast;
using Shouldly;
using Xunit;
using Column = HybridDb.Config.Column;

namespace HybridDb.Tests
{
    public class DocumentStoreTests : HybridDbAutoInitializeTests
    {
        readonly byte[] documentAsByteArray;
        readonly byte[] otherDocumentAsByteArray;

        public DocumentStoreTests()
        {
            documentAsByteArray = new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'};
            otherDocumentAsByteArray = new[] {(byte) 'l', (byte) 'a', (byte) 'r', (byte) 's'};
        }

        [Fact]
        public void CanInsert()
        {
            Document<Entity>().With(x => x.Field);

            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new {Field = "Asger", Document = documentAsByteArray});

            var row = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((string) row.Id).ShouldBe(id);
            ((Guid) row.Etag).ShouldNotBe(Guid.Empty);
            Encoding.ASCII.GetString((byte[]) row.Document).ShouldBe("asger");
            ((string) row.Field).ShouldBe("Asger");
        }

        [Fact]
        public void CanInsertDynamically()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            store.Insert(new DynamicDocumentTable("Entities"), id, new { Field = "Asger", Document = documentAsByteArray });

            var row = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((string) row.Id).ShouldBe(id);
            ((Guid) row.Etag).ShouldNotBe(Guid.Empty);
            Encoding.ASCII.GetString((byte[]) row.Document).ShouldBe("asger");
            ((string) row.Field).ShouldBe("Asger");
        }

        [Fact(Skip = "This will fail on first insert now, but we might want to check it at configuration time, but only if other stores do not support either.")]
        public void FailsOnSettingUpComplexProjections()
        {
            Should.Throw<ArgumentException>(() =>
            {
                Document<Entity>().With(x => x.Complex);
            });
        }

        [Fact]
        public void FailsOnDynamicallyInsertedComplexProjections()
        {
            Document<Entity>();
            
            Should.Throw<ArgumentException>(() =>
                store.Insert(new DynamicDocumentTable("Entities"), NewId(), new { Complex = new Entity.ComplexType() }));
        }

        [Fact(Skip = "Feature on hold")]
        public void CanInsertCollectionProjections()
        {
            Document<Entity>().With(x => x.Children.Select(y => y.NestedProperty));
            
            var id = NewId();
            var schema = store.Configuration.GetDesignFor<Entity>();
            store.Insert(
                schema.Table, id,
                new
                {
                    Children = new[]
                    {
                        new {NestedProperty = "A"},
                        new {NestedProperty = "B"}
                    }
                });

            var mainrow = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((string)mainrow.Id).ShouldBe(id);

            var utilrows = store.Database.RawQuery<dynamic>("select * from #Entities_Children").ToList();
            utilrows.Count.ShouldBe(2);
            
            var utilrow = utilrows.First();
            ((string)utilrow.DocumentId).ShouldBe(id);
            ((string)utilrow.NestedString).ShouldBe("A");
        }

        [Fact]
        public void CanUpdate()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = store.Insert(table.Table, id, new {Field = "Asger"});

            store.Update(table.Table, id, etag, new {Field = "Lars"});

            var row = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe("Lars");
        }

        [Fact]
        public void CanUpdatePessimistically()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new {Field = "Asger", Document = new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'}});

            Should.NotThrow(() => store.Update(table.Table, id, Guid.NewGuid(), new {Field = "Lars"}, lastWriteWins: true));
        }

        [Fact]
        public void UpdateFailsWhenEtagDoesNotMatch()
        {
            Document<Entity>().With(x => x.Field);
                        
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            Should.Throw<ConcurrencyException>(() => store.Update(table.Table, id, Guid.NewGuid(), new {Field = "Lars"}));
        }

        [Fact]
        public void UpdateFailsWhenIdNotMatchAkaObjectDeleted()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var etag = Guid.NewGuid();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            Should.Throw<ConcurrencyException>(() => store.Update(table.Table, NewId(), etag, new {Field = "Lars"}));
        }

        [Fact]
        public void CanGet()
        {
            Document<Entity>().With(x => x.Field).With(x => x.Complex.ToString());
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = store.Insert(table.Table, id, new {Field = "Asger", ComplexToString = "AB", Document = documentAsByteArray});

            var row = store.Get(table.Table, id);
            row[table.Table.IdColumn].ShouldBe(id);
            row[table.Table.EtagColumn].ShouldBe(etag);
            row[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
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

            var row = store.Get(new DynamicDocumentTable("Entities"), id);
            row[table.Table.IdColumn].ShouldBe(id);
            row[table.Table.EtagColumn].ShouldBe(etag);
            row[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            row[table.Table["Field"]].ShouldBe("Asger");
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

            var rows = store.Query(
                new SelectStatement(
                new Select(),
                new From(new TableName(table.Table.Name)),
                new Where(new Comparison(
                    ComparisonOperator.NotEqual, 
                    new ColumnName(table.Table.Name, "Field"), 
                    new Constant(typeof(string), "Bjarne")))), 
                out stats).ToList();

            rows.Count.ShouldBe(2);
            var first = rows.Single(x => (string)x.Data[table.Table.IdColumn] == id1);
            first.Data[table.Table.EtagColumn].ShouldBe(etag1);
            first.Data[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            first.Data[table.Table["Field"]].ShouldBe("Asger");

            var second = rows.Single(x => (string)x.Data[table.Table.IdColumn] == id2);
            second.Data[table.Table.IdColumn].ShouldBe(id2);
            second.Data[table.Table.EtagColumn].ShouldBe(etag2);
            second.Data[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            second.Data[table.Table["Field"]].ShouldBe("Hans");
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

            rows = store.Query<ProjectionWithNestedProperty>(new SelectStatement(new From(new TableName(table.Table.Name))), out stats).ToList();

            rows.Single().Data.TheChildNestedDouble.ShouldBe(9.8d);
        }

        [Fact]
        public void CanQueryAndReturnAnonymousProjections()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();

            store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            var t = new {Field = ""};

            QueryStats stats = null;
            var methodInfo = (from method in store.GetType().GetMethods()
                              where method.Name == "Query" && method.IsGenericMethod
                              select method).Single().MakeGenericMethod(t.GetType());

            var rows = ((IEnumerable<dynamic>) methodInfo.Invoke(store, new object[] {table.Table, stats, null, "Field = @name", 0, 0, "", new {name = "Asger"}})).ToList();

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
            var rows = store.Query(new DynamicDocumentTable("Entities"), out stats, where: "Field = @name", parameters: new { name = "Asger" }).ToList();

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

            store.Database.RawQuery<dynamic>("select * from #Entities").Count().ShouldBe(0);
        }

        [Fact]
        public void CanDeletePessimistically()
        {
            Document<Entity>();
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table.Table, id, new { });

            Should.NotThrow(() => store.Delete(table.Table, id, Guid.NewGuid(), lastWriteWins: true));
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
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = store.Execute(new InsertCommand(table.Table, id1, new { Field = "A" }),
                                     new InsertCommand(table.Table, id2, new { Field = "B" }));

            var rows = store.Database.RawQuery<Guid>("select Etag from #Entities order by Field").ToList();
            rows.Count.ShouldBe(2);
            rows[0].ShouldBe(etag);
            rows[1].ShouldBe(etag);
        }

        [Fact]
        public void BatchesAreTransactional()
        {
            Document<Entity>().With(x => x.Field);
            
            var id1 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etagThatMakesItFail = Guid.NewGuid();
            try
            {
                store.Execute(new InsertCommand(table.Table, id1, new { Field = "A" }),
                              new UpdateCommand(table.Table, id1, etagThatMakesItFail, new { Field = "B" }, false));
            }
            catch (ConcurrencyException)
            {
                // ignore the exception and ensure that nothing was inserted
            }

            store.Database.RawQuery<dynamic>("select * from #Entities").Count().ShouldBe(0);
        }

        [Fact]
        public void DoesNotSplitBelow2000Params()
        {
            Document<Entity>().With(x => x.Field);

            var table = store.Configuration.GetDesignFor<Entity>();

            // the initial migrations might issue some requests
            var initialNumberOfRequest = store.NumberOfRequests;

            var commands = new List<DatabaseCommand>();
            for (var i = 0; i < 333; i++) // each insert i 6 params so 333 commands equals 1998 params, threshold is at 2000
                commands.Add(new InsertCommand(table.Table, NewId(), new { Field = "A", Document = documentAsByteArray }));

            store.Execute(commands.ToArray());

            (store.NumberOfRequests - initialNumberOfRequest).ShouldBe(1);
        }

        [Fact]
        public void SplitsAbove2000Params()
        {
            Document<Entity>().With(x => x.Field);

            var table = store.Configuration.GetDesignFor<Entity>();

            // the initial migrations might issue some requests
            var initialNumberOfRequest = store.NumberOfRequests;

            var commands = new List<DatabaseCommand>();
            for (var i = 0; i < 334; i++) // each insert i 6 params so 334 commands equals 2004 params, threshold is at 2000
                commands.Add(new InsertCommand(table.Table, NewId(), new { Field = "A", Document = documentAsByteArray }));


            (store.NumberOfRequests - initialNumberOfRequest).ShouldBe(2);
        }

        [Fact]
        public void CanStoreAndGetEnumProjection()
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
            var result = store.Query(table.Table, out stats, skip: 2, take: 5, orderby: "Number").ToList();

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
            var result = store.Query(table.Table, out stats, take: 2, orderby: "Number").ToList();

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
            var result = store.Query(table.Table, out stats, skip: 7, orderby: "Number").ToList();

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

            QueryStats stats;
            var result = store.Query(table.Table, out stats).ToList();

            result.Count.ShouldBe(1);
        }

        [Fact]
        public void CanGetStats()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                store.Insert(table.Table, NewId(), new { Property = i });

            QueryStats stats;
            store.Query(table.Table, out stats, where: "Property >= 5");

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

            QueryStats stats;
            store.Query(table.Table, out stats, where: "Property >= 5", skip: 1);

            stats.RetrievedResults.ShouldBe(4);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public void CanGetStatsWithNoResults()
        {
            Document<Entity>();
            
            var table = store.Configuration.GetDesignFor<Entity>();

            QueryStats stats;
            store.Query(table.Table, out stats);

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

            QueryStats stats;
            store.Query(table.Table, out stats, @orderby: "Property", skip: 1);
            
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
            store.Query(table.Table, out stats, where: "Property >= 5", skip: 10);

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
            store.Query(table.Table, out stats, where: "Property >= 5", take: 2);

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
            store.Query(table.Table, out stats, where: "Property >= 5", take: 20);

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
            var result = store.Query(table.Table, out stats, select: "Field", orderby: "Id", skip: 1, take:1).Single();

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
            var result = store.Query(table.Table, out stats, skip: 2, take: 2, orderby: "Field desc").ToList();

            var props = result.Select(x => x[table.Table["Field"]]).ToList();
            props[0].ShouldBe("3");
            props[1].ShouldBe("2");
        }

        [Fact]
        public void WillEnlistCommandsInAmbientTransactions()
        {
            Document<Entity>();

            var table = store.Configuration.GetDesignFor<Entity>();

            using (new TransactionScope())
            {
                store.Insert(table.Table, NewId(), new { });
                store.Insert(table.Table, NewId(), new { });

                // No tx complete here
            }

            store.Database.RawQuery<dynamic>("select * from #Entities").Count().ShouldBe(0);
        }

        [Fact]
        public void CanUseTempDb()
        {
            var prefix = Guid.NewGuid().ToString();

            UseTempDb();

            Action<Configuration> configurator = x =>
            {
                x.UseTableNamePrefix(prefix);
                x.Document<Case>();
            };

            using (var globalStore1 = DocumentStore.ForTesting(TableMode.UseTempDb, connectionString, configurator))
            {
                globalStore1.Initialize();

                var id = NewId();
                globalStore1.Insert(globalStore1.Configuration.GetDesignFor<Case>().Table, id, new { });

                using (var globalStore2 = DocumentStore.ForTesting(TableMode.UseTempDb, connectionString, configurator))
                {
                    globalStore2.Initialize();

                    var result = globalStore2.Get(globalStore2.Configuration.GetDesignFor<Case>().Table, id);

                    result.ShouldNotBe(null);
                }
            }
        }

        [Fact()]
        public void UtilityColsAreRemovedFromQueryResults()
        {
            Document<Entity>();

            var table = new DocumentTable("Entities");
            store.Insert(table, NewId(), new { Version = 1 });

            QueryStats stats;
            var result1 = store.Query(table, out stats, skip: 0, take: 2).Single();
            result1.ContainsKey(new Column("RowNumber", typeof(int))).ShouldBe(false);
            result1.ContainsKey(new Column("TotalResults", typeof(int))).ShouldBe(false);

            var result2 = store.Query<object>(table, out stats, skip: 0, take: 2).Single();
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
            UseTempDb();

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
        public void CanJoinTablesOneToOne()
        {
            Document<Entity>();
            Document<OtherEntity>().With(x => x.EntityId);

            InitializeStore();

            var table1 = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table1.Table, "a", new { Document = new byte[] { 1 } });
            store.Insert(table1.Table, "b", new { Document = new byte[] { 2 } });
            
            var table2 = store.Configuration.GetDesignFor<OtherEntity>();
            store.Insert(table2.Table, "x", new { EntityId = "a", Document = new byte[] { 3 } });
            store.Insert(table2.Table, "y", new { EntityId = "b", Document = new byte[] { 4 } });

            QueryStats stats;

            var rows = store.Query(
                new SelectStatement(
                new Select(),
                new From(new TableName(table1.Table.Name), 
                    new Join(new TableName(table2.Table.Name), 
                        new Comparison(ComparisonOperator.Equal, 
                            new ColumnName(table1.Table.Name, "Id"),
                            new ColumnName(table2.Table.Name, "EntityId"))))),
                out stats).ToList();

            rows.Count.ShouldBe(2);
            rows[0].Data["Id"].ShouldBe("a");
            rows[0].Data["Document"].ShouldBe(new byte[] { 1 });
            rows[0].Data["OtherEntities_Id"].ShouldBe("x");
            rows[0].Data["OtherEntities_EntityId"].ShouldBe("a");
            rows[0].Data["OtherEntities_Document"].ShouldBe(new byte[] { 3 });

            rows[1].Data["Id"].ShouldBe("b");
            rows[1].Data["Document"].ShouldBe(new byte[] { 2 });
            rows[1].Data["OtherEntities_Id"].ShouldBe("y");
            rows[1].Data["OtherEntities_EntityId"].ShouldBe("b");
            rows[1].Data["OtherEntities_Document"].ShouldBe(new byte[] { 4 });
        }

        [Fact]
        public void CanJoinTablesOneToMany()
        {
            Document<Entity>();
            Document<OtherEntity>().With(x => x.EntityId);

            InitializeStore();

            var table1 = store.Configuration.GetDesignFor<Entity>();
            store.Insert(table1.Table, "a", new { Document = new byte[] { 1 } });
            
            var table2 = store.Configuration.GetDesignFor<OtherEntity>();
            store.Insert(table2.Table, "x", new { EntityId = "a", Document = new byte[] { 3 } });
            store.Insert(table2.Table, "y", new { EntityId = "a", Document = new byte[] { 4 } });

            QueryStats stats;

            var rows = store.Query(
                new SelectStatement(
                new Select(),
                new From(new TableName(table1.Table.Name), 
                    new Join(new TableName(table2.Table.Name), 
                        new Comparison(ComparisonOperator.Equal, 
                            new ColumnName(table1.Table.Name, "Id"),
                            new ColumnName(table2.Table.Name, "EntityId"))))),
                out stats).ToList();

            rows.Count.ShouldBe(2);
            rows[0].Data["Id"].ShouldBe("a");
            rows[0].Data["Document"].ShouldBe(new byte[] { 1 });
            rows[0].Data["OtherEntities_Id"].ShouldBe("x");
            rows[0].Data["OtherEntities_EntityId"].ShouldBe("a");
            rows[0].Data["OtherEntities_Document"].ShouldBe(new byte[] { 3 });

            rows[1].Data["Id"].ShouldBe("a");
            rows[1].Data["Document"].ShouldBe(new byte[] { 1 });
            rows[1].Data["OtherEntities_Id"].ShouldBe("y");
            rows[1].Data["OtherEntities_EntityId"].ShouldBe("a");
            rows[1].Data["OtherEntities_Document"].ShouldBe(new byte[] { 4 });

            //TODO: do we need this optimization?
            //ReferenceEquals(rows[0]["Document"], rows[1]["Document"]).ShouldBe(true);
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

        public class ProjectionWithNonProjectedField
        {
            public string NonProjectedField { get; set; }
        }

        public class EntityIndex
        {
            public string StringProp { get; set; }
        }

        public class ThrowingHybridDbExtension : IHybridDbExtension
        {
            public void OnRead(Table table, IDictionary<string, object> projections)
            {
                throw new OperationException();
            }

            public class OperationException : Exception { }
        }

        public class CountingSerializer : ISerializer
        {
            readonly ISerializer serializer;

            public CountingSerializer(ISerializer serializer)
            {
                this.serializer = serializer;
            }

            public int DeserializeCount { get; private set; }

            public byte[] Serialize(object obj)
            {
                return serializer.Serialize(obj);
            }

            public object Deserialize(byte[] data, Type type)
            {
                DeserializeCount++;
                return serializer.Deserialize(data, type);
            }
        }
    }

}