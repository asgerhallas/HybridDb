using System;
using HybridDb.Linq;
using Xunit;
using System.Linq;
using Shouldly;

namespace HybridDb.Tests
{
    public class LinqTests
    {
        readonly IDocumentSession session;

        public LinqTests()
        {
            session = new DocumentSession(new DocumentStore(null));
        }

        [Fact]
        public void CanQueryUsingQueryComprehensionSyntax()
        {
            var translation = (from a in session.Query<Entity>()
                          where a.Property == 2
                          select a).Translate();

            translation.Where.ShouldBe("(Property = 2)");
        }

        [Fact]
        public void CanQueryAll()
        {
            var translation = session.Query<Entity>().Translate();
            translation.Select.ShouldBe("");
            translation.Where.ShouldBe("");
            translation.Take.ShouldBe(0);
            translation.Skip.ShouldBe(0);
        }

        [Fact]
        public void CanQueryWithWhereEquals()
        {
            var translation = session.Query<Entity>().Where(x => x.Property == 2).Translate();
            translation.Where.ShouldBe("(Property = 2)");
        }

        [Fact]
        public void CanQueryWithWhereGreaterThan()
        {
            var translation = session.Query<Entity>().Where(x => x.Property > 1).Translate();
            translation.Where.ShouldBe("(Property > 1)");
        }

        [Fact]
        public void CanQueryWithWhereGreaterThanOrEqual()
        {
            var translation = session.Query<Entity>().Where(x => x.Property >= 2).Translate();
            translation.Where.ShouldBe("(Property >= 2)");
        }

        [Fact]
        public void CanQueryWithWhereLessThanOrEqual()
        {
            var translation = session.Query<Entity>().Where(x => x.Property <= 2).Translate();
            translation.Where.ShouldBe("(Property <= 2)");
        }

        [Fact]
        public void CanQueryWithWhereLessThan()
        {
            var translation = session.Query<Entity>().Where(x => x.Property < 2).Translate();
            translation.Where.ShouldBe("(Property < 2)");
        }

        [Fact]
        public void CanQueryWithWhereNotEquals()
        {
            var translation = session.Query<Entity>().Where(x => x.Property != 2).Translate();
            translation.Where.ShouldBe("(Property <> 2)");
        }

        [Fact]
        public void CanQueryWithWhereNull()
        {
            var translation = session.Query<Entity>().Where(x => x.StringProp == null).Translate();
            translation.Where.ShouldBe("(StringProp IS NULL)");
        }

        [Fact]
        public void CanQueryWithWhereNotNull()
        {
            var translation = session.Query<Entity>().Where(x => x.StringProp != null).Translate();
            translation.Where.ShouldBe("(StringProp IS NOT NULL)");
        }

        [Fact]
        public void CanQueryWithLocalVars()
        {
            int prop = 2;
            var translation = session.Query<Entity>().Where(x => x.Property == prop).Translate();
            translation.Where.ShouldBe("(Property = 2)");
        }

        [Fact]
        public void CanQueryWithNestedLocalVars()
        {
            var someObj = new { prop = 2 };
            var translation = session.Query<Entity>().Where(x => x.Property == someObj.prop).Translate();
            translation.Where.ShouldBe("(Property = 2)");
        }

        [Fact]
        public void CanQueryWithWhereAndNamedProjection()
        {
            var queryable = session.Query<Entity>().Where(x => x.Property == 2).AsProjection<ProjectedEntity>();
            var translation = queryable.Translate();

            queryable.ShouldBeTypeOf<IQueryable<ProjectedEntity>>();
            queryable.Provider.ShouldBeTypeOf<QueryProvider<Entity>>();
            translation.Select.ShouldBe("");
            translation.Where.ShouldBe("(Property = 2)");
        }

        [Fact]
        public void CanQueryWithOnlyNamedProjection()
        {
            var queryable = session.Query<Entity>().AsProjection<ProjectedEntity>();
            queryable.ShouldBeTypeOf<IQueryable<ProjectedEntity>>();
            queryable.Provider.ShouldBeTypeOf<QueryProvider<Entity>>();
        }

        [Fact]
        public void CanQueryOnNestedProperties()
        {
            var translation = session.Query<Entity>().Where(x => x.TheChild.NestedProperty > 2).Translate();
            translation.Where.ShouldBe("(TheChildNestedProperty > 2)");
        }

        [Fact]
        public void CanQueryWithSelect()
        {
            var translation = session.Query<Entity>().Select(x => new { x.Property }).Translate();
            translation.Select.ShouldBe("Property AS Property");
        }

        [Fact]
        public void CanQueryWithSelectToOtherName()
        {
            var translation = session.Query<Entity>().Select(x => new { HansOgGrethe = x.Property }).Translate();
            translation.Select.ShouldBe("Property AS HansOgGrethe");
        }

        [Fact]
        public void CanQueryWithSkipAndTake()
        {
            var translation = session.Query<Entity>().Skip(1).Take(1).Translate();
            translation.Skip.ShouldBe(1);
            translation.Take.ShouldBe(1);
        }

        [Fact]
        public void CanOrderBy()
        {
            var translation = session.Query<Entity>().OrderBy(x => x.Property).Translate();
            translation.OrderBy.ShouldBe("Property");
        }

        [Fact]
        public void CanOrderByAndThenBy()
        {
            var translation = session.Query<Entity>().OrderBy(x => x.Property).ThenBy(x => x.StringProp).Translate();
            translation.OrderBy.ShouldBe("Property, StringProp");
        }
        
        [Fact]
        public void CanOrderByDescending()
        {
            var translation = session.Query<Entity>().OrderByDescending(x => x.Property).Translate();
            translation.OrderBy.ShouldBe("Property DESC");
        }
        
        [Fact]
        public void CanOrderByAndThenByDescending()
        {
            var translation = session.Query<Entity>().OrderBy(x => x.Property).ThenByDescending(x => x.StringProp).Translate();
            translation.OrderBy.ShouldBe("Property, StringProp DESC");
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