using System;
using System.Linq;
using HybridDb.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class LinqIntegrationTests
    {
        readonly string connectionString;
        readonly DocumentStore store;
        readonly IDocumentSession session;

        public LinqIntegrationTests()
        {
            connectionString = "data source=.;Integrated Security=True";
            store = DocumentStore.ForTesting(connectionString);
            store.ForDocument<Entity>()
                 .Projection(x => x.Property)
                 .Projection(x => x.StringProp)
                 .Projection(x => x.TheChild.NestedProperty);
            store.Configuration.UseSerializer(new DefaultJsonSerializer());
            store.Initialize();

            session = store.OpenSession();
            session.Store(new Entity { Id = Guid.NewGuid(), Property = 1, StringProp = "Asger" });
            session.Store(new Entity { Id = Guid.NewGuid(), Property = 2, StringProp = "Lars", TheChild = new Entity.Child { NestedProperty = 3.1 } });
            session.Store(new Entity { Id = Guid.NewGuid(), Property = 3, StringProp = null });
            session.SaveChanges();
        }

        public void Dispose()
        {
            session.Dispose();
            store.Dispose();
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
        public void CanQueryWithNamedProjection()
        {
            var result = session.Query<Entity>().AsProjection<ProjectedEntity>().ToList();
            result.Count.ShouldBe(3);
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
            stats.TotalRows.ShouldBe(3);
        }

        [Fact]
        public void CanGetQueryStatsWhenUsingProjections()
        {
            QueryStats stats;
            session.Query<Entity>().Statistics(out stats).Select(x => new { x.Property }).ToList();
            stats.TotalRows.ShouldBe(3);
        }

        public class Entity
        {
            public Entity()
            {
                TheChild = new Child();
            }

            public string Field;
            public Guid Id { get; set; }
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
 
    }
}