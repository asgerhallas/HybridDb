using System;
using System.Linq;
using HybridDb.Linq;
using Shouldly;
using Xunit;

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

            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
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
            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereGreaterThan()
        {
            var translation = session.Query<Entity>().Where(x => x.Property > 1).Translate();
            translation.Where.ShouldBe("(Property > @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryWithWhereGreaterThanOrEqual()
        {
            var translation = session.Query<Entity>().Where(x => x.Property >= 2).Translate();
            translation.Where.ShouldBe("(Property >= @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereLessThanOrEqual()
        {
            var translation = session.Query<Entity>().Where(x => x.Property <= 2).Translate();
            translation.Where.ShouldBe("(Property <= @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereLessThan()
        {
            var translation = session.Query<Entity>().Where(x => x.Property < 2).Translate();
            translation.Where.ShouldBe("(Property < @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereNotEquals()
        {
            var translation = session.Query<Entity>().Where(x => x.Property != 2).Translate();
            translation.Where.ShouldBe("(Property <> @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
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
        public void CanQueryWithWhereAnd()
        {
            var translation = session.Query<Entity>().Where(x => x.Property == 2 && x.StringProp == "Lars").Translate();
            translation.Where.ShouldBe("((Property = @Value0) AND (StringProp = @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", "Lars");
        }

        [Fact]
        public void CanQueryWithWhereOr()
        {
            var translation = session.Query<Entity>().Where(x => x.Property == 2 || x.StringProp == "Lars").Translate();
            translation.Where.ShouldBe("((Property = @Value0) OR (StringProp = @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", "Lars");
        }

        [Fact]
        public void CanQueryWithWhereBitwiseAnd()
        {
            var translation = session.Query<Entity>().Where(x => (x.Property & 2) == 0).Translate();
            translation.Where.ShouldBe("((Property&@Value0) = @Value1)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", 0);
        }

        [Fact]
        public void CanQueryWithWhereBitwiseOr()
        {
            var translation = session.Query<Entity>().Where(x => (x.Property | 2) == 0).Translate();
            translation.Where.ShouldBe("((Property|@Value0) = @Value1)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", 0);
        }

        [Fact]
        public void CanQueryWithMultipleWhereClauses()
        {
            var translation = session.Query<Entity>().Where(x => x.Property == 0).Where(x => x.StringProp == "Lars").Translate();
            translation.Where.ShouldBe("((Property = @Value0) AND (StringProp = @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 0);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", "Lars");
        }

        [Fact]
        public void CanQueryWithMultipleSelectClauses()
        {
            var translation = session.Query<Entity>().Select(x => new { x.Id, x.StringProp }).Select(x => new { x.Id }).Translate();
            translation.Select.ShouldBe("Id AS Id");
        }

        [Fact]
        public void CanQueryWithParantheses()
        {
            var translation = session.Query<Entity>().Where(x => x.Property == 0 || (x.StringProp == null && x.Property == 1)).Translate();
            translation.Where.ShouldBe("((Property = @Value0) OR ((StringProp IS NULL) AND (Property = @Value1)))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 0);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", 1);
        }

        [Fact]
        public void CanQueryWithStartsWith()
        {
            var translation = session.Query<Entity>().Where(x => x.StringProp.StartsWith("L")).Translate();
            translation.Where.ShouldBe("(StringProp LIKE @Value0 + '%')");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", "L");
        }

        [Fact]
        public void CanQueryWithTypeConversion()
        {
            var translation = session.Query<Entity>().Where(x => x.NullableProperty == 2).Translate();
            translation.Where.ShouldBe("(NullableProperty = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithLocalVars()
        {
            var prop = 2;
            var translation = session.Query<Entity>().Where(x => x.Property == prop).Translate();
            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithNestedLocalVars()
        {
            var someObj = new {prop = 2};
            var translation = session.Query<Entity>().Where(x => x.Property == someObj.prop).Translate();
            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithNestedLocalVarsWithNullValue()
        {
            var someObj = new {prop = (int?)null};
            var translation = session.Query<Entity>().Where(x => x.Property == someObj.prop).Translate();
            translation.Where.ShouldBe("(Property IS NULL)");
        }

        [Fact]
        public void CanQueryWithNestedLocalVarsWithNullTarget()
        {
            ProjectedEntity someObj = null;
            Should.Throw<NullReferenceException>(() => session.Query<Entity>().Where(x => x.Property == someObj.Property).Translate());
        }

        [Fact]
        public void CanQueryWithWhereAndNamedProjection()
        {
            var queryable = session.Query<Entity>().Where(x => x.Property == 2).AsProjection<ProjectedEntity>();
            var translation = queryable.Translate();

            queryable.ShouldBeTypeOf<IQueryable<ProjectedEntity>>();
            queryable.Provider.ShouldBeTypeOf<QueryProvider<Entity>>();
            translation.Select.ShouldBe("");
            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
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
            translation.Where.ShouldBe("(TheChildNestedProperty > @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2.0);
        }

        [Fact]
        public void CanQueryWithSelectToAnonymous()
        {
            var translation = session.Query<Entity>().Select(x => new {x.Property}).Translate();
            translation.Select.ShouldBe("Property AS Property");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousWithMultipleProperties()
        {
            var translation = session.Query<Entity>().Select(x => new {x.Property, x.StringProp}).Translate();
            translation.Select.ShouldBe("Property AS Property, StringProp AS StringProp");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousWithNestedProperty()
        {
            var translation = session.Query<Entity>().Select(x => new {x.TheChild.NestedProperty}).Translate();
            translation.Select.ShouldBe("TheChildNestedProperty AS NestedProperty");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousToOtherName()
        {
            var translation = session.Query<Entity>().Select(x => new {HansOgGrethe = x.Property}).Translate();
            translation.Select.ShouldBe("Property AS HansOgGrethe");
        }

        [Fact]
        public void CanQueryWithSelectToNamed()
        {
            var translation = session.Query<Entity>().Select(x => new ProjectedEntity {Property = x.Property}).Translate();
            translation.Select.ShouldBe("Property AS Property");
        }

        [Fact]
        public void CanQueryWithSelectToNamedTypeWithNestedProperty()
        {
            var translation = session.Query<Entity>().Select(x => new ProjectedEntity { TheChildNestedProperty = x.TheChild.NestedProperty }).Translate();
            translation.Select.ShouldBe("TheChildNestedProperty AS TheChildNestedProperty");
        }


        [Fact]
        public void CanQueryWithSelectToNamedWithOtherName()
        {
            var translation = session.Query<Entity>().Select(x => new ProjectedEntity {StringProp = x.Field}).Translate();
            translation.Select.ShouldBe("Field AS StringProp");
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

        [Fact]
        public void CanWriteGuid()
        {
            var translation = session.Query<Entity>().Where(x => x.Id == Guid.Empty).Translate();
            translation.Where.ShouldBe("(Id = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", Guid.Empty);
        }

        [Fact]
        public void CanQueryWhereWithBool()
        {
            var translation = session.Query<Entity>().Where(x => x.BoolProp).Translate();
            translation.Where.ShouldBe("(BoolProp = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryWhereWithNotBool()
        {
            var translation = session.Query<Entity>().Where(x => !x.BoolProp).Translate();
            translation.Where.ShouldBe(" NOT (BoolProp = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryWhereWithBoolEquals()
        {
            bool something = true;
            var translation = session.Query<Entity>().Where(x => x.BoolProp == something).Translate();
            translation.Where.ShouldBe("(BoolProp = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", true);
        }

        bool WackyCustomEqualityCheck(int x, int y)
        {
            return x == y;
        }

        [Fact]
        public void CanQueryWhereWithConstantMethodCall()
        {
            var translation = session.Query<Entity>().Where(x => x.BoolProp == WackyCustomEqualityCheck(1, 1)).Translate();
            translation.Where.ShouldBe("(BoolProp = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryWhereWithInArrayConstant()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var list = new[] {guid1, guid2};
            var translation = session.Query<Entity>().Where(x => x.Id.In(list)).Translate();
            translation.Where.ShouldBe("(Id IN (@Value0, @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", guid1);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithInArrayInitialized()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var translation = session.Query<Entity>().Where(x => x.Id.In(new[] {guid1, guid2})).Translate();
            translation.Where.ShouldBe("(Id IN (@Value0, @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", guid1);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithEmptyIn()
        {
            var translation = session.Query<Entity>().Where(x => x.Id.In(new Guid[0])).Translate();
            translation.Where.ShouldBe("(Id IN (NULL))");
        }

        public class Entity
        {
            public string Field;

            public Entity()
            {
                TheChild = new Child();
            }

            public Guid Id { get; set; }
            public int Property { get; set; }
            public int? NullableProperty { get; set; }
            public string StringProp { get; set; }
            public DateTime DateTimeProp { get; set; }
            public bool BoolProp { get; set; }
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