using System;
using System.Linq;
using HybridDb.Linq.Old;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class DocumentSession_DateOnlyTests : HybridDbTests
    {
        public DocumentSession_DateOnlyTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Store_Load()
        {
            using (var session = store.OpenSession())
            {
                session.Store("a",
                    new EntityWith<DateOnly>
                    {
                        Prop = new DateOnly(2022, 1, 2)
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var loaded = session.Load<EntityWith<DateOnly>>("a");

                loaded.Prop.ShouldBe(new DateOnly(2022, 1, 2));
            }
        }

        [Fact]
        public void Store_Load_Nullable()
        {
            using (var session = store.OpenSession())
            {
                session.Store("a",
                    new EntityWith<DateOnly?>
                    {
                        Prop = null
                    });

                session.Store("b",
                    new EntityWith<DateOnly?>
                    {
                        Prop = new DateOnly(2022, 1, 2)
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var loadedA = session.Load<EntityWith<DateOnly?>>("a");
                var loadedB = session.Load<EntityWith<DateOnly?>>("b");

                loadedA.Prop.ShouldBe(null);
                loadedB.Prop.ShouldBe(new DateOnly(2022, 1, 2));
            }
        }

        [Fact]
        public void Store_Query()
        {
            using (var session = store.OpenSession())
            {
                session.Store("a",
                    new EntityWith<DateOnly>
                    {
                        Prop = new DateOnly(2022, 1, 2)
                    });

                session.Store("b",
                    new EntityWith<DateOnly>
                    {
                        Prop = new DateOnly(1877, 12, 12)
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var loaded = session.Query<EntityWith<DateOnly>>().OrderBy(x => x.Column<string>("Id")).ToList();

                loaded[0].Prop.ShouldBe(new DateOnly(2022, 1, 2));
                loaded[1].Prop.ShouldBe(new DateOnly(1877, 12, 12));
            }
        }

        [Fact]
        public void Store_Query_Select()
        {
            Document<EntityWith<DateOnly>>()
                .With(x => x.Prop);

            using (var session = store.OpenSession())
            {
                session.Store("a",
                    new EntityWith<DateOnly>
                    {
                        Prop = new DateOnly(2022, 1, 2)
                    });

                session.Store("b",
                    new EntityWith<DateOnly>
                    {
                        Prop = new DateOnly(1877, 12, 12)
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var loaded = session.Query<EntityWith<DateOnly>>()
                    .Select(x => new { x.Prop })
                    .OrderBy(x => x.Column<string>("Id"))
                    .ToList();

                loaded[0].Prop.ShouldBe(new DateOnly(2022, 1, 2));
                loaded[1].Prop.ShouldBe(new DateOnly(1877, 12, 12));
            }
        }

        [Fact]
        public void Store_Query_Select_Nullable()
        {
            Document<EntityWith<DateOnly?>>()
                .With(x => x.Prop);

            using (var session = store.OpenSession())
            {
                session.Store("a",
                    new EntityWith<DateOnly?>
                    {
                        Prop = new DateOnly(2022, 1, 2)
                    });

                session.Store("b",
                    new EntityWith<DateOnly?>
                    {
                        Prop = null
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var loaded = session.Query<EntityWith<DateOnly?>>()
                    .Select(x => new { x.Prop })
                    .OrderBy(x => x.Column<string>("Id"))
                    .ToList();

                loaded[0].Prop.ShouldBe(new DateOnly(2022, 1, 2));
                loaded[1].Prop.ShouldBe(null);
            }
        }

        [Fact]
        public void Store_Query_Where()
        {
            Document<EntityWith<DateOnly>>()
                .With(x => x.Prop);

            using (var session = store.OpenSession())
            {
                session.Store("a",
                    new EntityWith<DateOnly>
                    {
                        Prop = new DateOnly(2022, 1, 2)
                    });

                session.Store("b",
                    new EntityWith<DateOnly>
                    {
                        Prop = new DateOnly(1877, 12, 12)
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var loaded = session
                    .Query<EntityWith<DateOnly>>()
                    .Single(x => x.Prop > new DateOnly(1899, 1, 1));

                loaded.Prop.ShouldBe(new DateOnly(2022, 1, 2));
            }
        }

        [Fact]
        public void Store_Query_OrderBy()
        {
            Document<EntityWith<DateOnly>>()
                .With(x => x.Prop);

            using (var session = store.OpenSession())
            {
                session.Store("a",
                    new EntityWith<DateOnly>
                    {
                        Prop = new DateOnly(2022, 1, 2)
                    });

                session.Store("b",
                    new EntityWith<DateOnly>
                    {
                        Prop = new DateOnly(1877, 12, 12)
                    });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var loaded = session
                    .Query<EntityWith<DateOnly>>()
                    .OrderBy(x => x.Prop)
                    .ToList();

                loaded[0].Prop.ShouldBe(new DateOnly(1877, 12, 12));
                loaded[1].Prop.ShouldBe(new DateOnly(2022, 1, 2));
            }
        }
    }
}