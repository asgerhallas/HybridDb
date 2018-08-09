using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Dapper;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentStoreTests : HybridDbAutoInitializeTests
    {
        readonly byte[] documentAsByteArray;

        public DocumentStoreTests()
        {
            documentAsByteArray = new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'};
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

        [Fact(Skip = "We will maybe not support this in the future. Just get the table from QuerySchema and use that, when it can return DocumentTable and not just Table.")]
        public void CanInsertNullsDynamically()
        {
            Document<Entity>().With(x => x.Field);

            store.Insert(new DynamicDocumentTable("Entities"), NewId(), new Dictionary<string, object> {{"Field", null}});

            var row = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((string) row.Field).ShouldBe(null);
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

        [Fact(Skip ="We will maybe not support this in the future. Just get the table from QuerySchema and use that, when it can return DocumentTable and not just Table.")]
        public void CanUpdateDynamically()
        {
            Document<Entity>().With(x => x.Field).With(x => x.Property);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = store.Insert(table.Table, id, new {Field = "Asger"});

            // Maybe it should not be required to be a DocumentTable. If we do that everything should part of the projection. 
            // If we do not do that, why do we have document as part of the projection? Either or.
            store.Update(new DynamicDocumentTable("Entities"), id, etag, new Dictionary<string, object> { { "Field", null }, { "Property", "Lars" } });

            var row = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe(null);
            ((string) row.Property).ShouldBe("Lars");
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
        public void UpdateFailsWhenEtagNotMatch()
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
            var first = rows.Single(x => (string)x[table.Table.IdColumn] == id1);
            first[table.Table.EtagColumn].ShouldBe(etag1);
            first[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            first[table.Table["Field"]].ShouldBe("Asger");

            var second = rows.Single(x => (string)x[table.Table.IdColumn] == id2);
            second[table.Table.IdColumn].ShouldBe(id2);
            second[table.Table.EtagColumn].ShouldBe(etag2);
            second[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            second[table.Table["Field"]].ShouldBe("Hans");
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
                              where method.Name == "Query" && method.IsGenericMethod && method.GetParameters().Length == 9
                              select method).Single().MakeGenericMethod(t.GetType());

            var rows = ((IEnumerable<dynamic>) methodInfo.Invoke(store, new object[] {table.Table, stats, null, "Field = @name", 0, 0, "", false, new {name = "Asger"}})).ToList();

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

            store.Query<object>(table.Table, out _).Count().ShouldBe(0);
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

        //[Fact]
        //public void DoesNotSplitBelow2000Params()
        //{
        //    Document<Entity>().With(x => x.Field);

        //    var table = store.Configuration.GetDesignFor<Entity>();

        //    // the initial migrations might issue some requests
        //    var initialNumberOfRequest = store.NumberOfRequests;

        //    var commands = new List<DatabaseCommand>();
        //    for (var i = 0; i < 285; i++) // each insert i 7 params so 285 commands equals 1995 params, threshold is at 2000
        //        commands.Add(new InsertCommand(table.Table, NewId(), new { Field = "A", Document = documentAsByteArray }));

        //    store.Execute(commands.ToArray());

        //    (store.NumberOfRequests - initialNumberOfRequest).ShouldBe(1);
        ////}

        //[Fact]
        //public void SplitsAbove2000Params()
        //{
        //    Document<Entity>().With(x => x.Field);

        //    var table = store.Configuration.GetDesignFor<Entity>();

        //    // the initial migrations might issue some requests
        //    var initialNumberOfRequest = store.NumberOfRequests;

        //    var commands = new List<DatabaseCommand>();
        //    for (var i = 0; i < 286; i++) // each insert i 7 params so 286 commands equals 2002 params, threshold is at 2000
        //        commands.Add(new InsertCommand(table.Table, NewId(), new { Field = "A", Document = documentAsByteArray }));

        //    store.Execute(commands.ToArray());

        //    (store.NumberOfRequests - initialNumberOfRequest).ShouldBe(2);
        //}

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

            // the tempdb connection does not currently delete it's own tables
            // database.QuerySchema().ShouldNotContainKey("Cases");
        }

        [Fact()]
        public void UtilityColsAreRemovedFromQueryResults()
        {
            Document<Entity>();

            var table = new DocumentTable("Entities");
            store.Insert(table, NewId(), new { Version = 1 });

            var result1 = store.Query(table, out _, skip: 0, take: 2).Single();
            result1.ContainsKey(new Column("RowNumber", typeof(int))).ShouldBe(false);
            result1.ContainsKey(new Column("TotalResults", typeof(int))).ShouldBe(false);

            var result2 = store.Query<object>(table, out _, skip: 0, take: 2).Single();
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
        public void QueueInserts()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            store.Insert(table, NewId(), new { Property = "first" });
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();

            store.Insert(table, NewId(), new { Property = "second" });
            var results2 = store.Query<string>(table, results1[0].RowVersion, "Property").ToList();

            results2.Count.ShouldBe(1);
            results2[0].Data.ShouldBe("second");
            results2[0].LastOperation.ShouldBe(Operation.Inserted);
        }

        [Fact]
        public void QueueUpdates()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = store.Insert(table, id, new { Property = "first" });
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();

            store.Update(table, id, etag1, new { Property = "second" });
            var results2 = store.Query<string>(table, results1[0].RowVersion, "Property").ToList();

            results2.Count.ShouldBe(1);
            results2[0].Data.ShouldBe("second");
            results2[0].LastOperation.ShouldBe(Operation.Updated);
        }

        [Fact]
        public void QueueDeletes()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = store.Insert(table, id, new { Property = "first" });
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();

            store.Delete(table, id, etag1);

            var results2 = store.Query<string>(table, results1[0].RowVersion, "Property").ToList();

            results2.Count.ShouldBe(1);
            results2[0].Data.ShouldBe("first");
            results2[0].LastOperation.ShouldBe(Operation.Deleted);
        }

        [Fact]
        public void CanReinsertDeleted()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = store.Insert(table, id, new { Property = "first" });
            store.Delete(table, id, etag1);
            store.Insert(table, id, new { Property = "second" });

            var results2 = store.Query<string>(table, new byte[8], "Property").ToList();

            results2.Count.ShouldBe(2);
            results2[0].Data.ShouldBe("first");
            results2[0].LastOperation.ShouldBe(Operation.Deleted);
            results2[1].Data.ShouldBe("second");
            results2[1].LastOperation.ShouldBe(Operation.Inserted);
        }

        [Fact]
        public void CanRedeleteReinserted()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = store.Insert(table, id, new { Property = "first" });
            store.Delete(table, id, etag1);
            var etag2 = store.Insert(table, id, new { Property = "second" });
            store.Delete(table, id, etag2);

            var results2 = store.Query<string>(table, new byte[8], "Property").ToList();

            results2.Count.ShouldBe(2);
            results2[0].Data.ShouldBe("first");
            results2[0].LastOperation.ShouldBe(Operation.Deleted);
            results2[1].Data.ShouldBe("second");
            results2[1].LastOperation.ShouldBe(Operation.Deleted);
        }

        [Fact]
        public async void Bug_RaceConditionWithSnapshotAndRowVersion()
        {
            //Nummeret til rowversion kolonnen tildeles ved starten af tx, hvilket betyder at ovenstående giver følgende situation:

            //1. Tx1 starter og row A opdateres.Får tildelt rowversion = 1.
            //    2. Tx2 starter og row B opdateres. Får tildelt rowversion = 2 og comittes.
            //3. Tx3 starter og læser højeste nuværende rowversion, som er 2 og kører sin opdatering og gemmer sidst læste version som 2. 
            //4. Tx1 comittes og har stadig rowversion 1, men næste gang vi forespørger efter opdateringer, så kigger vi kun på rowversions højere end 2.
            //   Derfor misser vi Tx1 opdateringen.

            //Se https://stackoverflow.com/questions/28444599/implementing-incremental-client-updates-with-rowversions-in-postgres for detaljer.

            //Løsningen er at bruge `min-active-rowversion` til sætte et øvre grænse for hvilken version, der må læses.Så i ovenstående tilfælde vil `min-active-rowversion` være 1
            //og derfor vil vi kun læse op til rowversion 1, hvilket vil sige at opdatering 2 først kan blive læst, når 1 er comitted.

            var snapshot = new TransactionOptions { IsolationLevel = IsolationLevel.Snapshot };
            var readCommitted = new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted };

            UseRealTables();
            UseTableNamePrefix(nameof(Bug_RaceConditionWithSnapshotAndRowVersion));

            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id1 = NewId();
            var id2 = NewId();

            // the race condition only happens on updates, because inserts locks the primary key index and thus is not concurrent
            var etag1 = store.Insert(table, id1, new { Property = "first" });
            var etag2 = store.Insert(table, id2, new { Property = "second" });

            // get the initial row version after insert
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();
            var lastSeenRowVersion = results1[0].RowVersion;

            using (var tx1 = new TransactionScope(TransactionScopeOption.RequiresNew, readCommitted, TransactionScopeAsyncFlowOption.Enabled))
            {
                store.Update(table, id1, etag1, new { Property = "first updated" });

                using (var tx2 = new TransactionScope(TransactionScopeOption.RequiresNew, readCommitted, TransactionScopeAsyncFlowOption.Enabled))
                {
                    store.Update(table, id2, etag2, new { Property = "second updated" });

                    tx2.Complete();
                }

                using (new TransactionScope(TransactionScopeOption.RequiresNew, snapshot, TransactionScopeAsyncFlowOption.Enabled))
                {
                    // get latest completed updates
                    var results2 = store.Query<string>(table, lastSeenRowVersion, "Property").ToList();

                    // the query should not return anything when the race condition is fixed, but before fixing
                    // it will return the "second update" and lastSeenRowVersion will be set to a too high number
                    // so "first update" will never be seen.
                    if (results2.Any())
                    {
                        lastSeenRowVersion = results2[0].RowVersion;
                    }
                }

                tx1.Complete();
            }

            using (new TransactionScope(TransactionScopeOption.RequiresNew, snapshot, TransactionScopeAsyncFlowOption.Enabled))
            {
                // now that both updates are fully complete, expect to see them both - nothing skipped.
                var results3 = store.Query<string>(table, lastSeenRowVersion, "Property").ToList();

                results3.Count.ShouldBe(2);
                results3[0].Data.ShouldBe("first updated");
                results3[1].Data.ShouldBe("second updated");
            }
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