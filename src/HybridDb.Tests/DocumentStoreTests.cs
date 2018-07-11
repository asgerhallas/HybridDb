using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using HybridDb.Commands;
using HybridDb.Config;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentStoreTests : HybridDbAutoInitializeTests
    {
        readonly byte[] documentAsByteArray;

        public DocumentStoreTests() => 
            documentAsByteArray = new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'};

        [Fact]
        public async Task CanInsert()
        {
            Document<Entity>().With(x => x.Field);

            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, id, new {Field = "Asger", Document = documentAsByteArray});

            var row = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((string) row.Id).ShouldBe(id);
            ((Guid) row.Etag).ShouldNotBe(Guid.Empty);
            Encoding.ASCII.GetString((byte[]) row.Document).ShouldBe("asger");
            ((string) row.Field).ShouldBe("Asger");
        }

        [Fact]
        public async Task CanInsertDynamically()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            await store.Insert(new DynamicDocumentTable("Entities"), id, new { Field = "Asger", Document = documentAsByteArray });

            var row = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((string) row.Id).ShouldBe(id);
            ((Guid) row.Etag).ShouldNotBe(Guid.Empty);
            Encoding.ASCII.GetString((byte[]) row.Document).ShouldBe("asger");
            ((string) row.Field).ShouldBe("Asger");
        }

        [Fact(Skip = "We will maybe not support this in the future. Just get the table from QuerySchema and use that, when it can return DocumentTable and not just Table.")]
        public async Task CanInsertNullsDynamically()
        {
            Document<Entity>().With(x => x.Field);

            await store.Insert(new DynamicDocumentTable("Entities"), NewId(), new Dictionary<string, object> {{"Field", null}});

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
        public async Task CanInsertCollectionProjections()
        {
            Document<Entity>().With(x => x.Children.Select(y => y.NestedProperty));
            
            var id = NewId();
            var schema = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(
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
        public async Task CanUpdate()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = await store.Insert(table.Table, id, new {Field = "Asger"});

            await store.Update(table.Table, id, etag, new {Field = "Lars"});

            var row = store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe("Lars");
        }

        [Fact(Skip ="We will maybe not support this in the future. Just get the table from QuerySchema and use that, when it can return DocumentTable and not just Table.")]
        public async Task CanUpdateDynamically()
        {
            Document<Entity>().With(x => x.Field).With(x => x.Property);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = await store.Insert(table.Table, id, new {Field = "Asger"});

            // Maybe it should not be required to be a DocumentTable. If we do that everything should part of the projection. 
            // If we do not do that, why do we have document as part of the projection? Either or.
            await store.Update(new DynamicDocumentTable("Entities"), id, etag, new Dictionary<string, object> { { "Field", null }, { "Property", "Lars" } });

            var row = await store.Database.RawQuery<dynamic>("select * from #Entities").Single();
            ((Guid) row.Etag).ShouldNotBe(etag);
            ((string) row.Field).ShouldBe(null);
            ((string) row.Property).ShouldBe("Lars");
        }

        [Fact]
        public async Task CanUpdatePessimistically()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, id, new {Field = "Asger", Document = new[] {(byte) 'a', (byte) 's', (byte) 'g', (byte) 'e', (byte) 'r'}});

            Should.NotThrow(() => store.Update(table.Table, id, Guid.NewGuid(), new {Field = "Lars"}, lastWriteWins: true));
        }

        [Fact]
        public async Task UpdateFailsWhenEtagNotMatch()
        {
            Document<Entity>().With(x => x.Field);
                        
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            Should.Throw<ConcurrencyException>(() => store.Update(table.Table, id, Guid.NewGuid(), new {Field = "Lars"}));
        }

        [Fact]
        public async Task UpdateFailsWhenIdNotMatchAkaObjectDeleted()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var etag = Guid.NewGuid();
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            Should.Throw<ConcurrencyException>(() => store.Update(table.Table, NewId(), etag, new {Field = "Lars"}));
        }

        [Fact]
        public async Task CanGet()
        {
            Document<Entity>().With(x => x.Field).With(x => x.Complex.ToString());
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = await store.Insert(table.Table, id, new {Field = "Asger", ComplexToString = "AB", Document = documentAsByteArray});

            var row = await store.Get(table.Table, id);
            row[table.Table.IdColumn].ShouldBe(id);
            row[table.Table.EtagColumn].ShouldBe(etag);
            row[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            row[table.Table["Field"]].ShouldBe("Asger");
            row[table.Table["ComplexToString"]].ShouldBe("AB");
        }

        [Fact]
        public async Task CanGetDynamically()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = await store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            var row = await store.Get(new DynamicDocumentTable("Entities"), id);
            row[table.Table.IdColumn].ShouldBe(id);
            row[table.Table.EtagColumn].ShouldBe(etag);
            row[table.Table.DocumentColumn].ShouldBe(documentAsByteArray);
            row[table.Table["Field"]].ShouldBe("Asger");
        }

        [Fact]
        public async Task CanQueryProjectToNestedProperty()
        {
            Document<Entity>().With(x => x.TheChild.NestedDouble);
            
            var id1 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, id1, new { TheChildNestedDouble = 9.8d });

            var rows = store.Query<ProjectionWithNestedProperty>(table.Table, out _).ToList();

            rows.Single().Data.TheChildNestedDouble.ShouldBe(9.8d);
        }

        [Fact]
        public async Task CanQueryAndReturnFullDocuments()
        {
            Document<Entity>().With(x => x.Field);
            
            var id1 = NewId();
            var id2 = NewId();
            var id3 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag1 = await store.Insert(table.Table, id1, new { Field = "Asger", Document = documentAsByteArray });
            var etag2 = await store.Insert(table.Table, id2, new { Field = "Hans", Document = documentAsByteArray });
            await store.Insert(table.Table, id3, new { Field = "Bjarne", Document = documentAsByteArray });

            QueryStats stats;
            var rows = store.Query(table.Table, out stats, where: "Field != @name", parameters: new { name = "Bjarne" }).ToList();

            rows.Count.ShouldBe(2);
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
        public async Task CanQueryAndReturnAnonymousProjections()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();

            await store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

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
        public async Task CanQueryAndReturnValueProjections()
        {
            Document<Entity>().With(x => x.Field);
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();

            await store.Insert(table.Table, id, new { Field = "Asger", Document = documentAsByteArray });

            QueryStats stats;
            var rows = store.Query<string>(table.Table, out stats, select: "Field").Select(x => x.Data).ToList();

            Assert.Equal("Asger", rows.Single());
        }

        [Fact]
        public async Task CanQueryDynamicTable()
        {
            Document<Entity>().With(x => x.Field).With(x => x.Property);
            
            var id1 = NewId();
            var id2 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, id1, new { Field = "Asger", Property = "A", Document = documentAsByteArray });
            await store.Insert(table.Table, id2, new { Field = "Hans", Property = "B", Document = documentAsByteArray });

            QueryStats stats;
            var rows = store.Query(new DynamicDocumentTable("Entities"), out stats, where: "Field = @name", parameters: new { name = "Asger" }).ToList();

            rows.Count.ShouldBe(1);
            var row = rows.Single();
            row[table.Table["Field"]].ShouldBe("Asger");
            row[table.Table["Property"]].ShouldBe("A");
        }

        [Fact]
        public async Task CanDelete()
        {
            Document<Entity>();
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = await store.Insert(table.Table, id, new { });

            await store.Delete(table.Table, id, etag);

            store.Query<object>(table.Table, out _).Count().ShouldBe(0);
        }

        [Fact]
        public async Task CanDeletePessimistically()
        {
            Document<Entity>();
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, id, new { });

            Should.NotThrow(async () => await store.Delete(table.Table, id, Guid.NewGuid(), lastWriteWins: true));
        }

        [Fact]
        public async Task DeleteFailsWhenEtagNotMatch()
        {
            Document<Entity>();
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, id, new { });

            Should.Throw<ConcurrencyException>(async () => await store.Delete(table.Table, id, Guid.NewGuid()));
        }

        [Fact]
        public async Task DeleteFailsWhenIdNotMatchAkaDocumentAlreadyDeleted()
        {
            Document<Entity>();
            
            var id = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = await store.Insert(table.Table, id, new { });

            Should.Throw<ConcurrencyException>(async () => await store.Delete(table.Table, NewId(), etag)); 
        }

        [Fact]
        public async Task CanBatchCommandsAndGetEtag()
        {
            Document<Entity>().With(x => x.Field);
            
            var id1 = NewId();
            var id2 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etag = await store.Execute(new InsertCommand(table.Table, id1, new { Field = "A" }),
                                     new InsertCommand(table.Table, id2, new { Field = "B" }));

            var rows = store.Database.RawQuery<Guid>("select Etag from #Entities order by Field").ToList();
            rows.Count.ShouldBe(2);
            rows[0].ShouldBe(etag);
            rows[1].ShouldBe(etag);
        }

        [Fact]
        public async Task BatchesAreTransactional()
        {
            Document<Entity>().With(x => x.Field);
            
            var id1 = NewId();
            var table = store.Configuration.GetDesignFor<Entity>();
            var etagThatMakesItFail = Guid.NewGuid();
            try
            {
                await store.Execute(new InsertCommand(table.Table, id1, new { Field = "A" }),
                              new UpdateCommand(table.Table, id1, etagThatMakesItFail, new { Field = "B" }, false));
            }
            catch (ConcurrencyException)
            {
                // ignore the exception and ensure that nothing was inserted
            }

            store.Database.RawQuery<dynamic>("select * from #Entities").Count().ShouldBe(0);
        }

        //[Fact]
        //public async Task DoesNotSplitBelow2000Params()
        //{
        //    Document<Entity>().With(x => x.Field);

        //    var table = store.Configuration.GetDesignFor<Entity>();

        //    // the initial migrations might issue some requests
        //    var initialNumberOfRequest = store.NumberOfRequests;

        //    var commands = new List<DatabaseCommand>();
        //    for (var i = 0; i < 285; i++) // each insert i 7 params so 285 commands equals 1995 params, threshold is at 2000
        //        commands.Add(new InsertCommand(table.Table, NewId(), new { Field = "A", Document = documentAsByteArray }));

        //    await store.Execute(commands.ToArray());

        //    (store.NumberOfRequests - initialNumberOfRequest).ShouldBe(1);
        //}

        //[Fact]
        //public async Task SplitsAbove2000Params()
        //{
        //    Document<Entity>().With(x => x.Field);

        //    var table = store.Configuration.GetDesignFor<Entity>();

        //    // the initial migrations might issue some requests
        //    var initialNumberOfRequest = store.NumberOfRequests;

        //    var commands = new List<DatabaseCommand>();
        //    for (var i = 0; i < 286; i++) // each insert i 7 params so 286 commands equals 2002 params, threshold is at 2000
        //        commands.Add(new InsertCommand(table.Table, NewId(), new { Field = "A", Document = documentAsByteArray }));

        //    await store.Execute(commands.ToArray());

        //    (store.NumberOfRequests - initialNumberOfRequest).ShouldBe(2);
        //}

        [Fact]
        public async Task CanStoreAndQueryEnumProjection()
        {
            Document<Entity>().With(x => x.EnumProp);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            await store.Insert(table.Table, id, new { EnumProp = SomeFreakingEnum.Two });

            var result = await store.Get(table.Table, id);
            result[table.Table["EnumProp"]].ShouldBe(SomeFreakingEnum.Two.ToString());
        }

        [Fact]
        public async Task CanStoreAndQueryEnumProjectionToNetType()
        {
            Document<Entity>().With(x => x.EnumProp);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            await store.Insert(table.Table, id, new { EnumProp = SomeFreakingEnum.Two });

            QueryStats stats;
            var result = store.Query<ProjectionWithEnum>(table.Table, out stats).Single();
            result.Data.EnumProp.ShouldBe(SomeFreakingEnum.Two);
        }

        [Fact]
        public async Task CanStoreAndQueryStringProjection()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            await store.Insert(table.Table, id, new { Property = "Hest" });

            var result = await store.Get(table.Table, id);
            result[table.Table["Property"]].ShouldBe("Hest");
        }

        [Fact]
        public async Task CanStoreAndQueryOnNull()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            await store.Insert(table.Table, id, new { Property = (string)null });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, where: "(@Value IS NULL AND Property IS NULL) OR Property = @Value", parameters: new { Value = (string)null });
            result.Count().ShouldBe(1);
        }

        [Fact]
        public async Task CanStoreAndQueryDateTimeProjection()
        {
            Document<Entity>().With(x => x.DateTimeProp);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            var id = NewId();
            await store.Insert(table.Table, id, new { DateTimeProp = new DateTime(2001, 12, 24, 1, 1, 1) });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, where: "DateTimeProp = @dtp", parameters: new { dtp = new DateTime(2001, 12, 24, 1, 1, 1) });
            result.First()[table.Table["DateTimeProp"]].ShouldBe(new DateTime(2001, 12, 24, 1, 1, 1));
        }

        [Fact]
        public async Task CanPage()
        {
            Document<Entity>().With(x => x.Number);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                await store.Insert(table.Table, NewId(), new { Number = i });

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
        public async Task CanTake()
        {
            Document<Entity>().With(x => x.Number);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                await store.Insert(table.Table, NewId(), new { Number = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, take: 2, orderby: "Number").ToList();

            result.Count.ShouldBe(2);
            var props = result.Select(x => x[table.Table["Number"]]).ToList();
            props.ShouldContain(0);
            props.ShouldContain(1);
            stats.TotalResults.ShouldBe(10);
        }

        [Fact]
        public async Task CanSkip()
        {
            Document<Entity>().With(x => x.Number);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                await store.Insert(table.Table, NewId(), new { Number = i });

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
        public async Task CanQueryWithoutWhere()
        {
            Document<Entity>();
            
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, NewId(), new { });

            QueryStats stats;
            var result = store.Query(table.Table, out stats).ToList();

            result.Count.ShouldBe(1);
        }

        [Fact]
        public async Task CanGetStats()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                await store.Insert(table.Table, NewId(), new { Property = i });

            QueryStats stats;
            store.Query(table.Table, out stats, where: "Property >= 5");

            stats.RetrievedResults.ShouldBe(5);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public async Task CanGetStatsWhenSkipping()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                await store.Insert(table.Table, NewId(), new { Property = i });

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
        public async Task CanGetStatsWhenOrderingByPropertyWithSameValue()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            await store.Insert(table.Table, NewId(), new { Property = 10 });
            await store.Insert(table.Table, NewId(), new { Property = 10 });
            await store.Insert(table.Table, NewId(), new { Property = 10 });
            await store.Insert(table.Table, NewId(), new { Property = 10 });
            await store.Insert(table.Table, NewId(), new { Property = 11 });
            await store.Insert(table.Table, NewId(), new { Property = 11 });

            QueryStats stats;
            store.Query(table.Table, out stats, @orderby: "Property", skip: 1);
            
            stats.RetrievedResults.ShouldBe(5);
            stats.TotalResults.ShouldBe(6);
        }

        [Fact]
        public async Task CanGetStatsWhenSkippingAllOrMore()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                await store.Insert(table.Table, NewId(), new { Property = i });

            QueryStats stats;
            store.Query(table.Table, out stats, where: "Property >= 5", skip: 10);

            stats.RetrievedResults.ShouldBe(0);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public async Task CanGetStatsWhenTaking()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                await store.Insert(table.Table, NewId(), new { Property = i });

            QueryStats stats;
            store.Query(table.Table, out stats, where: "Property >= 5", take: 2);

            stats.RetrievedResults.ShouldBe(2);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public async Task CanGetStatsWhenTakingAllOrMore()
        {
            Document<Entity>().With(x => x.Property);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 0; i < 10; i++)
                await store.Insert(table.Table, NewId(), new { Property = i });

            QueryStats stats;
            store.Query(table.Table, out stats, where: "Property >= 5", take: 20);

            stats.RetrievedResults.ShouldBe(5);
            stats.TotalResults.ShouldBe(5);
        }

        [Fact]
        public async Task CanOrderBy()
        {
            Document<Entity>().With(x => x.Field);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 5; i > 0; i--)
                await store.Insert(table.Table, NewId(), new { Field = i });

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
        public async Task CanOrderByIdAndSelectOtherField()
        {
            Document<Entity>().With(x => x.Field);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 5; i > 0; i--)
                await store.Insert(table.Table, i.ToString(), new { Field = i });

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
        public async Task CanOrderByIdAndSelectOtherFieldWindowed()
        {
            Document<Entity>().With(x => x.Field);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 5; i > 0; i--)
                await store.Insert(table.Table, i.ToString(), new { Field = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, select: "Field", orderby: "Id", skip: 1, take:1).Single();

            result[table.Table["Field"]].ShouldBe("2");
        }

        [Fact]
        public async Task CanOrderByDescWhileSkippingAndTaking()
        {
            Document<Entity>().With(x => x.Field);
            
            var table = store.Configuration.GetDesignFor<Entity>();
            for (var i = 5; i > 0; i--)
                await store.Insert(table.Table, NewId(), new { Field = i });

            QueryStats stats;
            var result = store.Query(table.Table, out stats, skip: 2, take: 2, orderby: "Field desc").ToList();

            var props = result.Select(x => x[table.Table["Field"]]).ToList();
            props[0].ShouldBe("3");
            props[1].ShouldBe("2");
        }

        [Fact]
        public async Task WillEnlistCommandsInAmbientTransactions()
        {
            Document<Entity>();

            var table = store.Configuration.GetDesignFor<Entity>();

            using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                await store.Insert(table.Table, NewId(), new { });
                await store.Insert(table.Table, NewId(), new { });

                // No tx complete here
            }

            store.Database.RawQuery<dynamic>("select * from #Entities").Count().ShouldBe(0);
        }

        [Fact]
        public async Task CanUseTempDb()
        {
            var prefix = Guid.NewGuid().ToString();

            UseTempDb();

            void Configurator(Configuration x)
            {
                x.UseTableNamePrefix(prefix);
                x.Document<Case>();
            }

            using (var globalStore1 = DocumentStore.ForTesting(TableMode.UseTempDb, connectionString, Configurator))
            {
                globalStore1.Initialize();

                var id = NewId();
                await globalStore1.Insert(globalStore1.Configuration.GetDesignFor<Case>().Table, id, new { });

                using (var globalStore2 = DocumentStore.ForTesting(TableMode.UseTempDb, connectionString, Configurator))
                {
                    globalStore2.Initialize();

                    var result = await globalStore2.Get(globalStore2.Configuration.GetDesignFor<Case>().Table, id);

                    result.ShouldNotBe(null);
                }
            }

            // the tempdb connection does not currently delete it's own tables
            // database.QuerySchema().ShouldNotContainKey("Cases");
        }

        [Fact()]
        public async Task UtilityColsAreRemovedFromQueryResults()
        {
            Document<Entity>();

            var table = new DocumentTable("Entities");
            await store.Insert(table, NewId(), new { Version = 1 });

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

            Parallel.For(0, 10, async x =>
            {
                var table = new DocumentTable("Entities");
                await store.Insert(table, NewId(), new { Property = "Asger", Version = 1 });
            });
        }

        [Fact]
        public async Task QueueInserts()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            await store.Insert(table, NewId(), new { Property = "first" });
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();

            await store.Insert(table, NewId(), new { Property = "second" });
            var results2 = store.Query<string>(table, results1[0].RowVersion, "Property").ToList();

            results2.Count.ShouldBe(1);
            results2[0].Data.ShouldBe("second");
            results2[0].LastOperation.ShouldBe(Operation.Inserted);
        }

        [Fact]
        public async Task QueueUpdates()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = await store.Insert(table, id, new { Property = "first" });
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();

            await store.Update(table, id, etag1, new { Property = "second" });
            var results2 = store.Query<string>(table, results1[0].RowVersion, "Property").ToList();

            results2.Count.ShouldBe(1);
            results2[0].Data.ShouldBe("second");
            results2[0].LastOperation.ShouldBe(Operation.Updated);
        }

        [Fact]
        public async Task QueueDeletes()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = await store.Insert(table, id, new { Property = "first" });
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();

            await store.Delete(table, id, etag1);

            var results2 = store.Query<string>(table, results1[0].RowVersion, "Property").ToList();

            results2.Count.ShouldBe(1);
            results2[0].Data.ShouldBe("first");
            results2[0].LastOperation.ShouldBe(Operation.Deleted);
        }

        [Fact]
        public async Task CanReinsertDeleted()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = await store.Insert(table, id, new { Property = "first" });
            await store.Delete(table, id, etag1);
            await store.Insert(table, id, new { Property = "second" });

            var results2 = store.Query<string>(table, new byte[8], "Property").ToList();

            results2.Count.ShouldBe(2);
            results2[0].Data.ShouldBe("first");
            results2[0].LastOperation.ShouldBe(Operation.Deleted);
            results2[1].Data.ShouldBe("second");
            results2[1].LastOperation.ShouldBe(Operation.Inserted);
        }

        [Fact]
        public async Task CanRedeleteReinserted()
        {
            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = await store.Insert(table, id, new { Property = "first" });
            await store.Delete(table, id, etag1);
            var etag2 = await store.Insert(table, id, new { Property = "second" });
            await store.Delete(table, id, etag2);

            var results2 = store.Query<string>(table, new byte[8], "Property").ToList();

            results2.Count.ShouldBe(2);
            results2[0].Data.ShouldBe("first");
            results2[0].LastOperation.ShouldBe(Operation.Deleted);
            results2[1].Data.ShouldBe("second");
            results2[1].LastOperation.ShouldBe(Operation.Deleted);
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