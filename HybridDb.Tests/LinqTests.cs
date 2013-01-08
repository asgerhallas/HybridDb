using System;
using HybridDb.Linq;
using Xunit;
using System.Linq;
using Shouldly;

namespace HybridDb.Tests
{
    public class LinqTests
    {
        readonly string connectionString;
        readonly DocumentStore store;

        public LinqTests()
        {
            connectionString = "data source=.;Integrated Security=True";
            store = DocumentStore.ForTesting(connectionString);
            store.ForDocument<Entity>()
                 .Projection(x => x.Property)
                 .Projection(x => x.StringProp)
                 .Projection(x => x.TheChild.NestedProperty);
            store.Configuration.UseSerializer(new DefaultJsonSerializer());
            store.Initialize();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = Guid.NewGuid(), Property = 1, StringProp = "Asger"});
                session.Store(new Entity { Id = Guid.NewGuid(), Property = 2, StringProp = "Lars", TheChild = new Entity.Child { NestedProperty = 3.1 }});
                session.Store(new Entity { Id = Guid.NewGuid(), Property = 3, StringProp = null});
                session.SaveChanges();
            }
        }

        public void Dispose()
        {
            store.Dispose();
        }

        [Fact]
        public void CanQueryUsingQueryComprehensionSyntax()
        {
            using (var session = store.OpenSession())
            {
                var result = (from a in session.Query<Entity>()
                              where a.Property == 2
                              select a).ToList();

                result.Count().ShouldBe(1);
            }
        }

        [Fact]
        public void CanQueryAll()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().ToList();
                result.Count.ShouldBe(3);
            }
        }

        [Fact]
        public void CanQueryWithWhereEquals()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property == 2).ToList();
                result.Single().Property.ShouldBe(2);
            }
        }

        [Fact]
        public void CanQueryWithWhereGreaterThan()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property > 1).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 2);
                result.ShouldContain(x => x.Property == 3);
            }
        }

        [Fact]
        public void CanQueryWithWhereGreaterThanOrEqual()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property >= 2).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 2);
                result.ShouldContain(x => x.Property == 3);
            }
        }

        [Fact]
        public void CanQueryWithWhereLessThanOrEqual()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property <= 2).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 1);
                result.ShouldContain(x => x.Property == 2);
            }
        }

        [Fact]
        public void CanQueryWithWhereLessThan()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property < 2).ToList();
                result.Count.ShouldBe(1);
                result.ShouldContain(x => x.Property == 1);
            }
        }

        [Fact]
        public void CanQueryWithWhereNotEquals()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property != 2).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 1);
                result.ShouldContain(x => x.Property == 3);
            }
        }

        [Fact]
        public void CanQueryWithWhereNull()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.StringProp == null).ToList();
                result.Count.ShouldBe(1);
                result.ShouldContain(x => x.Property == 3);
            }
        }

        [Fact]
        public void CanQueryWithWhereNotNull()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.StringProp != null).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 1);
                result.ShouldContain(x => x.Property == 2);
            }
        }

        [Fact]
        public void CanQueryWithLocalVars()
        {
            using (var session = store.OpenSession())
            {
                int prop = 2;
                var result = session.Query<Entity>().Where(x => x.Property == prop).ToList();
                result.Count.ShouldBe(1);
                result.ShouldContain(x => x.Property == 2);
            }
        }

        [Fact]
        public void CanQueryWithWhereAndNamedProjection()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property == 2).AsProjection<ProjectedEntity>().ToList();
                var projection = result.Single();
                projection.ShouldBeTypeOf<ProjectedEntity>();
                projection.Property.ShouldBe(2);
                projection.StringProp.ShouldBe("Lars");
            }
        }

        [Fact]
        public void CanQueryWithOnlyNamedProjection()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().AsProjection<ProjectedEntity>().ToList();
                result.Count.ShouldBe(3);
            }
        }

        [Fact]
        public void CanQueryOnNestedProperties()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.TheChild.NestedProperty > 2).ToList();
                result.Count.ShouldBe(1);
                result.ShouldContain(x => Math.Abs(x.TheChild.NestedProperty - 3.1) < 0.1);
            }
        }

        [Fact]
        public void CanProjectToNestedProperties()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property == 2).AsProjection<ProjectedEntity>().ToList();
                result.Single().TheChildNestedProperty.ShouldBe(3.1);
            }
        }

        [Fact]
        public void CanQueryWithSelect()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Select(x => new { x.Property }).ToList();
                result.Select(x => x.Property).ShouldContain(1);
                result.Select(x => x.Property).ShouldContain(2);
                result.Select(x => x.Property).ShouldContain(3);
            }
        }

        [Fact]
        public void CanQueryWithSelectToOtherName()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Select(x => new { HansOgGrethe = x.Property }).ToList();
                result.Select(x => x.HansOgGrethe).ShouldContain(1);
                result.Select(x => x.HansOgGrethe).ShouldContain(2);
                result.Select(x => x.HansOgGrethe).ShouldContain(3);
            }
        }

        [Fact]
        public void CanQueryWithSkipAndTake()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Skip(1).Take(1).ToList();
                result.Count.ShouldBe(1);
                result.Single().Property.ShouldBe(2);
            }
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