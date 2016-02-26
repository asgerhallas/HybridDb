using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HybridDb.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class LinqTests : HybridDbTests
    {
        [Fact]
        public void CanQueryUsingQueryComprehensionSyntax()
        {
            var translation = (from a in Query<Entity>()
                               where a.Property == 2
                               select a).Translate();

            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryAll()
        {
            var translation = Query<Entity>().Translate();
            translation.Select.ShouldBe("");
            translation.Where.ShouldBe("");
            translation.Take.ShouldBe(0);
            translation.Skip.ShouldBe(0);
        }

        [Fact]
        public void CanQueryWithWhereEquals()
        {
            var translation = Query<Entity>().Where(x => x.Property == 2).Translate();
            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereGreaterThan()
        {
            var translation = Query<Entity>().Where(x => x.Property > 1).Translate();
            translation.Where.ShouldBe("(Property > @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryWithWhereGreaterThanOrEqual()
        {
            var translation = Query<Entity>().Where(x => x.Property >= 2).Translate();
            translation.Where.ShouldBe("(Property >= @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereLessThanOrEqual()
        {
            var translation = Query<Entity>().Where(x => x.Property <= 2).Translate();
            translation.Where.ShouldBe("(Property <= @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereLessThan()
        {
            var translation = Query<Entity>().Where(x => x.Property < 2).Translate();
            translation.Where.ShouldBe("(Property < @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereNotEquals()
        {
            var translation = Query<Entity>().Where(x => x.Property != 2).Translate();
            translation.Where.ShouldBe("(Property <> @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereNull()
        {
            var translation = Query<Entity>().Where(x => x.StringProp == null).Translate();
            translation.Where.ShouldBe("(StringProp IS NULL)");
        }

        [Fact]
        public void CanQueryWithWhereNotNull()
        {
            var translation = Query<Entity>().Where(x => x.StringProp != null).Translate();
            translation.Where.ShouldBe("(StringProp IS NOT NULL)");
        }

        [Fact]
        public void CanQueryWithWhereAnd()
        {
            var translation = Query<Entity>().Where(x => x.Property == 2 && x.StringProp == "Lars").Translate();
            translation.Where.ShouldBe("((Property = @Value0) AND (StringProp = @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", "Lars");
        }

        [Fact]
        public void CanQueryWithWhereOr()
        {
            var translation = Query<Entity>().Where(x => x.Property == 2 || x.StringProp == "Lars").Translate();
            translation.Where.ShouldBe("((Property = @Value0) OR (StringProp = @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", "Lars");
        }

        [Fact]
        public void CanQueryWithWhereBitwiseAnd()
        {
            var translation = Query<Entity>().Where(x => (x.Property & 2) == 0).Translate();
            translation.Where.ShouldBe("((Property&@Value0) = @Value1)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", 0);
        }

        [Fact]
        public void CanQueryWithWhereBitwiseOr()
        {
            var translation = Query<Entity>().Where(x => (x.Property | 2) == 0).Translate();
            translation.Where.ShouldBe("((Property|@Value0) = @Value1)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", 0);
        }

        [Fact]
        public void CanQueryWithMultipleWhereClauses()
        {
            var translation = Query<Entity>().Where(x => x.Property == 0).Where(x => x.StringProp == "Lars").Translate();
            translation.Where.ShouldBe("((Property = @Value0) AND (StringProp = @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 0);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", "Lars");
        }

        [Fact]
        public void CanQueryWithMultipleSelectClauses()
        {
            var translation = Query<Entity>().Select(x => new {x.Id, x.StringProp}).Select(x => new {x.Id}).Translate();
            translation.Select.ShouldBe("Id AS Id");
        }

        [Fact]
        public void CanQueryWithParantheses()
        {
            var translation = Query<Entity>().Where(x => x.Property == 0 || (x.StringProp == null && x.Property == 1)).Translate();
            translation.Where.ShouldBe("((Property = @Value0) OR ((StringProp IS NULL) AND (Property = @Value1)))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 0);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", 1);
        }

        [Fact]
        public void CanQueryWithStartsWith()
        {
            var translation = Query<Entity>().Where(x => x.StringProp.StartsWith("L")).Translate();
            translation.Where.ShouldBe("(StringProp LIKE @Value0 + '%')");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", "L");
        }

        [Fact]
        public void CanQueryWithContains()
        {
            var translation = Query<Entity>().Where(x => x.StringProp.Contains("L")).Translate();
            translation.Where.ShouldBe("(StringProp LIKE '%' + @Value0 + '%')");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", "L");
        }

        [Fact]
        public void CanQueryWithTypeConversion()
        {
            var translation = Query<Entity>().Where(x => x.NullableProperty == 2).Translate();
            translation.Where.ShouldBe("(NullableProperty = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithLocalVars()
        {
            var prop = 2;
            var translation = Query<Entity>().Where(x => x.Property == prop).Translate();
            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithNestedLocalVars()
        {
            var someObj = new {prop = 2};
            var translation = Query<Entity>().Where(x => x.Property == someObj.prop).Translate();
            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithNestedLocalVarsWithNullValue()
        {
            var someObj = new {prop = (int?) null};
            var translation = Query<Entity>().Where(x => x.Property == someObj.prop).Translate();
            translation.Where.ShouldBe("(Property IS NULL)");
        }

        [Fact]
        public void CanQueryWithNestedLocalVarsWithNullTarget()
        {
            ProjectedEntity someObj = null;
            Should.Throw<NullReferenceException>(() => Query<Entity>().Where(x => x.Property == someObj.Property).Translate());
        }

        [Fact]
        public void CanQueryWithWhereAndNamedProjection()
        {
            var queryable = Query<Entity>().Where(x => x.Property == 2).AsProjection<ProjectedEntity>();
            var translation = queryable.Translate();

            queryable.ShouldBeOfType<Query<ProjectedEntity>>();
            queryable.Provider.ShouldBeOfType<QueryProvider<Entity>>();
            translation.Select.ShouldBe("");
            translation.Where.ShouldBe("(Property = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithOnlyNamedProjection()
        {
            var queryable = Query<Entity>().AsProjection<ProjectedEntity>();
            queryable.ShouldBeOfType<Query<ProjectedEntity>>();
            queryable.Provider.ShouldBeOfType<QueryProvider<Entity>>();
        }

        [Fact]
        public void CanQueryOnNestedProperties()
        {
            var translation = Query<Entity>().Where(x => x.TheChild.NestedProperty > 2).Translate();
            translation.Where.ShouldBe("(TheChildNestedProperty > @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2.0);
        }

        [Fact]
        public void CanQueryOnComplexProperties()
        {
            var translation = Query<Entity>()
                .Where(x => x.Complex.GetType().GetProperties(BindingFlags.Static | BindingFlags.Instance).Any())
                .Translate();
            translation.Where.ShouldBe("(ComplexGetTypeGetPropertiesInstanceStaticAny = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryOnInsanelyComplexProperties()
        {
            var translation = Query<Entity>()
                .Where(x => x.Children.Where(child => child.NestedProperty < 10)
                    .Count(child => child.NestedProperty > 1) == 1)
                .Translate();

            translation.Where.ShouldBe("(ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1 = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryOnDynamicallyNamedProperties()
        {
            var translation = Query<Entity>().Where(x => x.Column<int>("SomeColumn") == 1).Translate();

            translation.Where.ShouldBe("(SomeColumn = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void FailsWhenCallingColumnOnAnythingButParameter()
        {
            Should.Throw<NotSupportedException>(() =>
                Query<Entity>().Where(x => x.Property.Column<int>("SomeColumn") == 1).Translate());
        }

        [Fact]
        public void CanQueryWithSelectToAnonymous()
        {
            var translation = Query<Entity>().Select(x => new {x.Property}).Translate();
            translation.Select.ShouldBe("Property AS Property");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousWithMultipleProperties()
        {
            var translation = Query<Entity>().Select(x => new {x.Property, x.StringProp}).Translate();
            translation.Select.ShouldBe("Property AS Property, StringProp AS StringProp");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousWithNestedProperty()
        {
            var translation = Query<Entity>().Select(x => new {x.TheChild.NestedProperty}).Translate();
            translation.Select.ShouldBe("TheChildNestedProperty AS NestedProperty");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousWithComplexProperty()
        {
            var translation = Query<Entity>()
                .Select(x => new
                {
                    Projection = x.Children.Where(child => child.NestedProperty < 10)
                        .Count(child => child.NestedProperty > 1)
                })
                .Translate();
            translation.Select.ShouldBe("ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1 AS Projection");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousWithDynamicallyNamedProperty()
        {
            var translation = Query<Entity>()
                .Select(x => new {Property = x.Column<int>("SomeProperty")})
                .Translate();
            translation.Select.ShouldBe("SomeProperty AS Property");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousToOtherName()
        {
            var translation = Query<Entity>().Select(x => new {HansOgGrethe = x.Property}).Translate();
            translation.Select.ShouldBe("Property AS HansOgGrethe");
        }

        [Fact]
        public void CanQueryWithSelectToNamed()
        {
            var translation = Query<Entity>().Select(x => new ProjectedEntity {Property = x.Property}).Translate();
            translation.Select.ShouldBe("Property AS Property");
        }

        [Fact]
        public void CanQueryWithSelectToNamedTypeWithNestedProperty()
        {
            var translation = Query<Entity>().Select(x => new ProjectedEntity {TheChildNestedProperty = x.TheChild.NestedProperty}).Translate();
            translation.Select.ShouldBe("TheChildNestedProperty AS TheChildNestedProperty");
        }

        [Fact]
        public void CanQueryWithSelectToNamedTypeWithComplexProperty()
        {
            var translation = Query<Entity>()
                .Select(x => new ProjectedEntity
                {
                    ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1 =
                        x.Children.Where(child => child.NestedProperty < 10).Count(child => child.NestedProperty > 1)
                })
                .Translate();
            translation.Select.ShouldBe(
                "ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1 AS ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1");
        }

        [Fact]
        public void CanQueryWithSelectToNamedTypeWithDynamicallyNamedProperty()
        {
            var translation = Query<Entity>()
                .Select(x => new ProjectedEntity
                {
                    Property = x.Column<int>("SomeProperty")
                })
                .Translate();

            translation.Select.ShouldBe("SomeProperty AS Property");
        }

        [Fact]
        public void CanQueryWithSelectToNamedWithOtherName()
        {
            var translation = Query<Entity>().Select(x => new ProjectedEntity {StringProp = x.Field}).Translate();
            translation.Select.ShouldBe("Field AS StringProp");
        }

        [Fact(Skip = "Feature tbd")]
        public void CanQueryWithTwoSelects()
        {
            var translation = Query<Entity>().Select(x => new {x.Field}).Select(x => x.Field).Translate();
            translation.Select.ShouldBe("Field AS Field");
        }

        [Fact(Skip = "Feature tbd")]
        public void CanQueryWithTwoSelects2()
        {
            var translation = Query<Entity>().Select(x => new {Something = x.Column<string>("Field")}).Select(x => x.Something).Translate();
            translation.Select.ShouldBe("Field AS Something");
        }

        [Fact]
        public void CanQueryWithSkipAndTake()
        {
            var translation = Query<Entity>().Skip(1).Take(1).Translate();
            translation.Skip.ShouldBe(1);
            translation.Take.ShouldBe(1);
        }

        [Fact]
        public void CanOrderBy()
        {
            var translation = Query<Entity>().OrderBy(x => x.Property).Translate();
            translation.OrderBy.ShouldBe("Property");
        }

        [Fact]
        public void CanOrderByAndThenBy()
        {
            var translation = Query<Entity>().OrderBy(x => x.Property).ThenBy(x => x.StringProp).Translate();
            translation.OrderBy.ShouldBe("Property, StringProp");
        }

        [Fact]
        public void CanOrderByDescending()
        {
            var translation = Query<Entity>().OrderByDescending(x => x.Property).Translate();
            translation.OrderBy.ShouldBe("Property DESC");
        }

        [Fact]
        public void CanOrderByAndThenByDescending()
        {
            var translation = Query<Entity>().OrderBy(x => x.Property).ThenByDescending(x => x.StringProp).Translate();
            translation.OrderBy.ShouldBe("Property, StringProp DESC");
        }

        [Fact]
        public void CanWriteGuid()
        {
            var translation = Query<Entity>().Where(x => x.Id == Guid.Empty).Translate();
            translation.Where.ShouldBe("(Id = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", Guid.Empty);
        }

        [Fact]
        public void CanQueryWhereWithBool()
        {
            var translation = Query<Entity>().Where(x => x.BoolProp).Translate();
            translation.Where.ShouldBe("(BoolProp = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryWhereWithNotBool()
        {
            var translation = Query<Entity>().Where(x => !x.BoolProp).Translate();
            translation.Where.ShouldBe(" NOT (BoolProp = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryWhereWithBoolEquals()
        {
            var something = true;
            var translation = Query<Entity>().Where(x => x.BoolProp == something).Translate();
            translation.Where.ShouldBe("(BoolProp = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryWhereWithConstantMethodCall()
        {
            var translation = Query<Entity>().Where(x => x.BoolProp == WackyCustomEqualityCheck(1, 1)).Translate();
            translation.Where.ShouldBe("(BoolProp = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", true);
        }

        bool WackyCustomEqualityCheck(int x, int y)
        {
            return x == y;
        }

        [Fact]
        public void CanQueryWhereWithConstantStaticMethodCall()
        {
            var translation = Query<Entity>().Where(x => StaticNoise(2) > 1).Translate();
            translation.Where.ShouldBe("(@Value0 > @Value1)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 2);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", 1);
        }

        static int StaticNoise(int arg)
        {
            return arg;
        }

        [Fact]
        public void CanQueryWhereWithInArrayConstant()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var list = new[] {guid1, guid2};
            var translation = Query<Entity>().Where(x => x.Id.In(list)).Translate();
            translation.Where.ShouldBe("(Id IN (@Value0, @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", guid1);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithInArrayInitialized()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var translation = Query<Entity>().Where(x => x.Id.In(new[] {guid1, guid2})).Translate();
            translation.Where.ShouldBe("(Id IN (@Value0, @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", guid1);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithInArrayToArrayed()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var list = new[] {guid1, guid2};
            var translation = Query<Entity>().Where(x => x.Id.In(list.ToArray())).Translate();
            translation.Where.ShouldBe("(Id IN (@Value0, @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", guid1);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithInEmptyArray()
        {
            // ReSharper disable once RedundantExplicitParamsArrayCreation
            var translation = Query<Entity>().Where(x => x.Id.In(new Guid[0])).Translate();
            translation.Where.ShouldBe("(@Value0 <> @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryWhereWithNotInEmptyArray()
        {
            // ReSharper disable once RedundantExplicitParamsArrayCreation
            var translation = Query<Entity>().Where(x => !x.Id.In(new Guid[0])).Translate();
            translation.Where.ShouldBe(" NOT (@Value0 <> @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryWhereWithNotInArray()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var list = new[] {guid1, guid2};
            var translation = Query<Entity>().Where(x => !x.Id.In(list)).Translate();
            translation.Where.ShouldBe(" NOT (Id IN (@Value0, @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", guid1);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithInOnUserDefinedColumn()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var list = new[] {guid1, guid2};
            var translation = Query<Entity>().Where(x => x.Column<Guid>("Id").In(list.ToArray())).Translate();
            translation.Where.ShouldBe("(Id IN (@Value0, @Value1))");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", guid1);
            translation.Parameters.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithTrueBoolConstant()
        {
            var translation = Query<Entity>().Where(x => true).Translate();
            translation.Where.ShouldBe("(@Value0 = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryWhereWithFalseBoolConstant()
        {
            var translation = Query<Entity>().Where(x => false).Translate();
            translation.Where.ShouldBe("(@Value0 <> @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryOnIndexes()
        {
            var translation = Query<Entity>().Where(x => x.Index<ExtIndex>().StringProp == "asger").Translate();

            translation.Where.ShouldBe("(StringProp = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", "asger");
        }

        [Fact(Skip = "Issue #30")]
        public void CanQueryEnums()
        {
            var translation = Query<Entity>().Where(x => x.Enum == Enumse.Second).Translate();

            translation.Where.ShouldBe("(Enum = @Value0)");
            translation.Parameters.ShouldContainKeyAndValue("@Value0", "Second");
        }

        [Fact]
        public void CanQueryOnUserDefinedColumnFromVariable()
        {
            var somecolumn = new Entity {StringProp = "SomeColumn"};
            var translation = Query<Entity>().OrderBy(x => x.Column<string>(somecolumn.StringProp.ToString())).Translate();

            translation.OrderBy.ShouldBe("SomeColumn");
        }

        Query<T> Query<T>() where T : class
        {
            var store = DocumentStore.ForTesting(TableMode.UseTempTables, connectionString);
            var session = new DocumentSession(store);
            return new Query<T>(new QueryProvider<T>(session, null));
        }

        public class Entity
        {
            public string Field;

            public Entity()
            {
                TheChild = new Child();
                Complex = new object();
                Children = new List<Child>();
            }

            public Guid Id { get; set; }
            public int Property { get; set; }
            public int? NullableProperty { get; set; }
            public string StringProp { get; set; }
            public DateTime DateTimeProp { get; set; }
            public bool BoolProp { get; set; }
            public Child TheChild { get; set; }
            public List<Child> Children { get; set; }
            public object Complex { get; set; }
            public Enumse Enum { get; set; }

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
            public int ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1 { get; set; }
        }

        public class ExtIndex
        {
            public string StringProp { get; set; }
        }

        public enum Enumse
        {
            None = 0,
            First = 1,
            Second = 2,
            Third = 4
        }
    }
}