using System;
using System.Linq;
using HybridDb.Linq;
using HybridDb.Serialization;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class LinqIntegrationTests : HybridDbTests
    {
        readonly IDocumentSession session;

        public LinqIntegrationTests()
        {
            Document<Entity>()
                .With(x => x.Property)
                .With(x => x.StringProp)
                .With(x => x.TheChild.NestedProperty);
            
            store.Configuration.UseSerializer(new DefaultSerializer());

            store.Initialize();

            session = Using(store.OpenSession());
            session.Store(new Entity { Id = NewId(), Property = 1, StringProp = "Asger" });
            session.Store(new Entity { Id = NewId(), Property = 2, StringProp = "Lars", TheChild = new Entity.Child { NestedProperty = 3.1 } });
            session.Store(new Entity { Id = NewId(), Property = 3, StringProp = null });
            session.SaveChangesAsync().Wait();
        }

        [Fact]
        public void CanQuery()
        {
            var result = session.Query<Entity>().ToList();
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void CanQueryWithWhere()
        {
            var result = session.Query<Entity>().Where(x => x.Property == 2).ToList();
            result.Single().Property.ShouldBe(2);
        }

        [Fact]
        public void CanQueryWithWhereAndProjection()
        {
            var result = session.Query<Entity>().Where(x => x.Property == 2).Select(x => new { x.StringProp }).ToList();
            result.Single().StringProp.ShouldBe("Lars");
        }

        [Fact]
        public void CanQueryWithNamedProjection()
        {
            var result = session.Query<Entity>().AsProjection<ProjectedEntity>().ToList();
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void CanQueryWithSelectToNamedTypeWithNestedProperty2()
        {
            Should.NotThrow(() => session.Query<Entity>().Select(x => new ProjectionWithPropertyContainingAs
            {
                CaseName = x.StringProp,
            }).ToList());
        }

        [Fact]
        public void CanQueryWithSelectToNamedTypeWithNestedProperty()
        {
            var result = session.Query<Entity>().Select(x => new ProjectedEntity
            {
                TheChildNestedProperty = x.TheChild.NestedProperty
            }).ToList();
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void CanQueryWithSelect()
        {
            var result = session.Query<Entity>().Select(x => new { x.Property }).ToList();
            result.Select(x => x.Property).ShouldContain(1);
            result.Select(x => x.Property).ShouldContain(2);
            result.Select(x => x.Property).ShouldContain(3);
        }

        [Fact]
        public void CanQueryWithSelectToTrackedType()
        {
            var result = session.Query<Entity>().Select(x => x).ToList();
            result.Count.ShouldBe(3);
        }

        [Fact]
        public void CanQueryWithSelectToOtherName()
        {
            var result = session.Query<Entity>().Select(x => new { HansOgGrethe = x.Property }).ToList();
            result.Select(x => x.HansOgGrethe).ShouldContain(1);
            result.Select(x => x.HansOgGrethe).ShouldContain(2);
            result.Select(x => x.HansOgGrethe).ShouldContain(3);
        }

        [Fact]
        public void CanQueryWithSkipAndTakeAndOrderBy()
        {
            var result = session.Query<Entity>().Skip(1).Take(1).OrderBy(x => x.Property).ToList();
            result.Count.ShouldBe(1);
            result.Single().Property.ShouldBe(2);
        }

        [Fact]
        public void CanGetQueryStats()
        {
            QueryStats stats;
            session.Query<Entity>().Statistics(out stats).ToList();
            stats.TotalResults.ShouldBe(3);
        }

        [Fact]
        public void CanGetQueryStatsWhenUsingProjections()
        {
            QueryStats stats;
            session.Query<Entity>().Statistics(out stats).Select(x => new { x.Property }).ToList();
            stats.TotalResults.ShouldBe(3);
        }

        [Fact]
        public void CanQueryWithWhereIn()
        {
            var result = session.Query<Entity>().Where(x => x.StringProp.In("Asger", "Lars")).ToList();
            result.Count.ShouldBe(2);
        }

        [Fact]
        public void CanQueryWithSingle()
        {
            var result = session.Query<Entity>().Where(x => x.StringProp == "Asger").Single();
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void CanQueryWithSingleWithPredicate()
        {
            var result = session.Query<Entity>().Single(x => x.StringProp == "Asger");
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void QueryWithSingleFailsWhenMoreThanOneResult()
        {
            Should.Throw<InvalidOperationException>(() => session.Query<Entity>().Single());
        }

        [Fact]
        public void QueryWithSingleFailsWhenZeroResult()
        {
            Should.Throw<InvalidOperationException>(() => session.Query<Entity>().Single(x => x.StringProp == "WuggaWugga"));
        }

        [Fact]
        public void CanQueryWithSingleOrDefault()
        {
            var result = session.Query<Entity>().Where(x => x.StringProp == "Asger").SingleOrDefault();
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void CanQueryWithSingleOrDefaultWithPredicate()
        {
            var result = session.Query<Entity>().SingleOrDefault(x => x.StringProp == "Asger");
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void QueryWithSingleOrDefaultFailsWhenMoreThanOneResult()
        {
            Should.Throw<InvalidOperationException>(() => session.Query<Entity>().SingleOrDefault());
        }

        [Fact]
        public void QueryWithSingleOrDefaultReturnsNullZeroResult()
        {
            session.Query<Entity>().SingleOrDefault(x => x.StringProp == "WuggaWugga").ShouldBe(null);
        }

        [Fact]
        public void CanQueryWithFirst()
        {
            var result = session.Query<Entity>().Where(x => x.StringProp == "Asger").First();
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void CanQueryWithFirstWithPredicate()
        {
            var result = session.Query<Entity>().First(x => x.StringProp == "Asger");
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void QueryWithFirstReturnsFirstWhenMoreThanOneResult()
        {
            var result = session.Query<Entity>().OrderBy(x => x.StringProp).Where(x => x.StringProp != null).First();
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void QueryWithFirstFailsWhenZeroResult()
        {
            Should.Throw<InvalidOperationException>(() => session.Query<Entity>().First(x => x.StringProp == "WuggaWugga"));
        }

        [Fact]
        public void CanQueryWithFirstOrDefault()
        {
            var result = session.Query<Entity>().Where(x => x.StringProp == "Asger").FirstOrDefault();
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void CanQueryWithFirstOrDefaultWithPredicate()
        {
            var result = session.Query<Entity>().FirstOrDefault(x => x.StringProp == "Asger");
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void QueryWithFirstOrDefaultReturnsFirstWhenMoreThanOneResult()
        {
            var result = session.Query<Entity>().OrderBy(x => x.StringProp).Where(x => x.StringProp != null).FirstOrDefault();
            result.ShouldNotBe(null);
            result.StringProp.ShouldBe("Asger");
        }

        [Fact]
        public void QueryWithFirstOrDefaultReturnsNullWhenZeroResult()
        {
            session.Query<Entity>().FirstOrDefault(x => x.StringProp == "WuggaWugga").ShouldBe(null);
        }

        public class Entity
        {
            public Entity()
            {
                TheChild = new Child();
            }

            public string Field;
            public string Id { get; set; }
            public int Property { get; set; }
            public string StringProp { get; set; }
            public DateTime DateTimeProp { get; set; }
            public Child TheChild { get; set; }

            public class Child
            {
                public double NestedProperty { get; set; }
            }
        }

        public class ProjectedEntity
        {
            public int Property { get; set; }
            public string StringProp { get; set; }
            public double TheChildNestedProperty { get; set; }
        }

        public class ProjectionWithPropertyContainingAs
        {
            public string CaseName { get; set; }
        }
    }
}