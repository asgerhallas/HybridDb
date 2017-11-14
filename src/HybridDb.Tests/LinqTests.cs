using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HybridDb.Linq;
using HybridDb.Linq2.Emitter;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class LinqTests : HybridDbTests
    {
        [Fact]
        public void CanQueryUsingQueryComprehensionSyntax()
        {
            var translation = Translate(
                from a in Query<Entity>()
                where a.Property == 2
                select a);

            translation.Where.ShouldBe("([Entities].[Property] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryAll()
        {
            var translation = Translate(Query<Entity>());
            translation.Select.ShouldBe("");
            translation.From.ShouldBe("[Entities]");
            translation.Where.ShouldBe("(1=1)");
            translation.Take.ShouldBe(0);
            translation.Skip.ShouldBe(0);
        }

        [Fact]
        public void CanQueryWithWhereEquals()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property == 2));
            translation.Where.ShouldBe("([Entities].[Property] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereGreaterThan()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property > 1));
            translation.Where.ShouldBe("([Entities].[Property] > @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryWithWhereGreaterThanOrEqual()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property >= 2));
            translation.Where.ShouldBe("([Entities].[Property] >= @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereLessThanOrEqual()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property <= 2));
            translation.Where.ShouldBe("([Entities].[Property] <= @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereLessThan()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property < 2));
            translation.Where.ShouldBe("([Entities].[Property] < @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereNotEquals()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property != 2));
            translation.Where.ShouldBe("([Entities].[Property] <> @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithWhereNull()
        {
            var translation = Translate(Query<Entity>().Where(x => x.StringProp == null));
            translation.Where.ShouldBe("([Entities].[StringProp] IS NULL)");
        }

        [Fact]
        public void CanQueryWithWhereNotNull()
        {
            var translation = Translate(Query<Entity>().Where(x => x.StringProp != null));
            translation.Where.ShouldBe("([Entities].[StringProp] IS NOT NULL)");
        }

        [Fact]
        public void CanQueryWithWhereAnd()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property == 2 && x.StringProp == "Lars"));
            translation.Where.ShouldBe("(([Entities].[Property] = @Value0) AND ([Entities].[StringProp] = @Value1))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", "Lars");
        }

        [Fact]
        public void CanQueryWithWhereOr()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property == 2 || x.StringProp == "Lars"));
            translation.Where.ShouldBe("(([Entities].[Property] = @Value0) OR ([Entities].[StringProp] = @Value1))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", "Lars");
        }

        [Fact]
        public void CanQueryWithWhereBitwiseAnd()
        {
            var translation = Translate(Query<Entity>().Where(x => (x.Property & 2) == 0));
            translation.Where.ShouldBe("(([Entities].[Property] & @Value0) = @Value1)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", 0);
        }

        [Fact]
        public void CanQueryWithWhereBitwiseOr()
        {
            var translation = Translate(Query<Entity>().Where(x => (x.Property | 2) == 0));
            translation.Where.ShouldBe("(([Entities].[Property] | @Value0) = @Value1)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", 0);
        }

        [Fact]
        public void CanQueryWithMultipleWhereClauses()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property == 0).Where(x => x.StringProp == "Lars"));
            translation.Where.ShouldBe("(([Entities].[Property] = @Value0) AND ([Entities].[StringProp] = @Value1))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 0);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", "Lars");
        }

        [Fact]
        public void CanQueryWithMultipleSelectClauses()
        {
            var translation = Translate(Query<Entity>().Select(x => new {x.Id, x.StringProp}).Select(x => new {x.Id}));
            translation.Select.ShouldBe("[Entities].[Id] AS [Id]");
        }

        [Fact]
        public void CanQueryWithMultipleSelectClausesWithRenaming()
        {
            var translation = Translate(Query<Entity>().Select(x => new {Yksi = x.Id}).Select(x => new {x.Yksi}));
            translation.Select.ShouldBe("[Entities].[Id] AS [Yksi]");
        }

        [Fact]
        public void CanQueryWithMultipleSelectClausesWithRenamingAgain()
        {
            var translation = Translate(Query<Entity>().Select(x => new {Yksi = x.Id}).Select(x => new {Kaksi = x.Yksi}));
            translation.Select.ShouldBe("[Entities].[Id] AS [Kaksi]");
        }

        [Fact]
        public void CanQueryWithParantheses()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property == 0 || (x.StringProp == null && x.Property == 1)));
            translation.Where.ShouldBe("(([Entities].[Property] = @Value0) OR (([Entities].[StringProp] IS NULL) AND ([Entities].[Property] = @Value1)))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 0);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", 1);
        }

        [Fact]
        public void CanQueryWithStartsWith()
        {
            var translation = Translate(Query<Entity>().Where(x => x.StringProp.StartsWith("L")));
            translation.Where.ShouldBe("([Entities].[StringProp] LIKE '' + @Value0 + '%')");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", "L");
        }

        [Fact]
        public void CanQueryWithContains()
        {
            var translation = Translate(Query<Entity>().Where(x => x.StringProp.Contains("L")));
            translation.Where.ShouldBe("([Entities].[StringProp] LIKE '%' + @Value0 + '%')");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", "L");
        }

        [Fact]
        public void CanQueryWithTypeConversion()
        {
            var translation = Translate(Query<Entity>().Where(x => x.NullableProperty == 2));
            translation.Where.ShouldBe("([Entities].[NullableProperty] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithLocalVars()
        {
            var prop = 2;
            var translation = Translate(Query<Entity>().Where(x => x.Property == prop));
            translation.Where.ShouldBe("([Entities].[Property] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithNestedLocalVars()
        {
            var someObj = new {prop = 2};
            var translation = Translate(Query<Entity>().Where(x => x.Property == someObj.prop));
            translation.Where.ShouldBe("([Entities].[Property] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithNestedLocalVarsWithNullValue()
        {
            var someObj = new {prop = (int?) null};
            var translation = Translate(Query<Entity>().Where(x => x.Property == someObj.prop));
            translation.Where.ShouldBe("([Entities].[Property] IS NULL)");
        }

        [Fact]
        public void CanQueryWithNestedLocalVarsWithNullTarget()
        {
            ProjectedEntity someObj = null;
            Should.Throw<NullReferenceException>(() => Translate(Query<Entity>().Where(x => x.Property == someObj.Property)));
        }

        [Fact]
        public void CanQueryWithWhereAndNamedProjection()
        {
            var queryable = Query<Entity>().Where(x => x.Property == 2).AsProjection<ProjectedEntity>();
            var translation = Translate(queryable);

            queryable.ShouldBeOfType<Query<ProjectedEntity>>();
            queryable.Provider.ShouldBeOfType<QueryProvider>();
            queryable.Provider.ShouldBeOfType<QueryProvider>();
            translation.Select.ShouldBe("");
            translation.Where.ShouldBe("([Entities].[Property] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
        }

        [Fact]
        public void CanQueryWithOnlyNamedProjection()
        {
            var queryable = Query<Entity>().AsProjection<ProjectedEntity>();
            queryable.ShouldBeOfType<Query<ProjectedEntity>>();
            queryable.Provider.ShouldBeOfType<QueryProvider>();
        }

        [Fact]
        public void CanQueryOnNestedProperties()
        {
            var translation = Translate(Query<Entity>().Where(x => x.TheChild.NestedProperty > 2));
            translation.Where.ShouldBe("([Entities].[TheChildNestedProperty] > @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2.0);
        }

        [Fact]
        public void CanQueryOnComplexProperties()
        {
            var translation = Translate(Query<Entity>()
                    .Where(x => x.Complex.GetType().GetProperties(BindingFlags.Static | BindingFlags.Instance).Any()));
            translation.Where.ShouldBe("([Entities].[ComplexGetTypeGetPropertiesInstanceStaticAny] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryOnInsanelyComplexProperties()
        {
            var translation = Translate(Query<Entity>()
                    .Where(x => x.Children.Where(child => child.NestedProperty < 10)
                        .Count(child => child.NestedProperty > 1) == 1));

            translation.Where.ShouldBe("([Entities].[ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryOnDynamicallyNamedProperties()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Column<int>("SomeColumn") == 1));

            translation.Where.ShouldBe("([Entities].[SomeColumn] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void FailsWhenCallingColumnOnAnythingButParameter()
        {
            Should.Throw<NotSupportedException>(() =>
                Translate(Query<Entity>().Where(x => x.Property.Column<int>("SomeColumn") == 1)));
        }

        [Fact]
        public void CanQueryWithSelectToAnonymous()
        {
            var translation = Translate(Query<Entity>().Select(x => new {x.Property}));
            translation.Select.ShouldBe("[Entities].[Property] AS Property");
        }


        [Fact]
        public void CanQueryWithSelectToAnonymousWithMultipleProperties()
        {
            var translation = Translate(Query<Entity>().Select(x => new {x.Property, x.StringProp}));
            translation.Select.ShouldBe("[Entities].[Property] AS Property, [Entities].[StringProp] AS StringProp");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousWithNestedProperty()
        {
            var translation = Translate(Query<Entity>().Select(x => new {x.TheChild.NestedProperty}));
            translation.Select.ShouldBe("[Entities].[TheChildNestedProperty] AS NestedProperty");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousWithComplexProperty()
        {
            var translation = Translate(Query<Entity>()
                    .Select(x => new
                    {
                        Projection = x.Children.Where(child => child.NestedProperty < 10)
                            .Count(child => child.NestedProperty > 1)
                    }));
            translation.Select.ShouldBe("[Entities].[ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1] AS Projection");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousWithDynamicallyNamedProperty()
        {
            var translation = Translate(Query<Entity>()
                    .Select(x => new {Property = x.Column<int>("SomeProperty")}));
            translation.Select.ShouldBe("[Entities].[SomeProperty] AS Property");
        }

        [Fact]
        public void CanQueryWithSelectToAnonymousToOtherName()
        {
            var translation = Translate(Query<Entity>().Select(x => new {HansOgGrethe = x.Property}));
            translation.Select.ShouldBe("[Entities].[Property] AS HansOgGrethe");
        }

        [Fact]
        public void CanQueryWithSelectToNamed()
        {
            var translation = Translate(Query<Entity>().Select(x => new ProjectedEntity {Property = x.Property}));
            translation.Select.ShouldBe("[Entities].[Property] AS Property");
        }

        [Fact]
        public void SelectedAliasesShouldNotBeUsedInOtherClauses()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Property == 1).Select(x => new { TheP = x.Property }));
            translation.Select.ShouldBe("[Entities].[Property] AS [TheP]");
            translation.Where.ShouldBe("([Entities].[Property] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 1);
        }


        [Fact]
        public void CanQueryWithSelectToNamedTypeWithNestedProperty()
        {
            var translation = Translate(Query<Entity>().Select(x => new ProjectedEntity {TheChildNestedProperty = x.TheChild.NestedProperty}));
            translation.Select.ShouldBe("[Entities].[TheChildNestedProperty] AS TheChildNestedProperty");
        }

        [Fact]
        public void CanQueryWithSelectToNamedTypeWithComplexProperty()
        {
            var translation = Translate(Query<Entity>()
                    .Select(x => new ProjectedEntity
                    {
                        ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1 =
                            x.Children.Where(child => child.NestedProperty < 10).Count(child => child.NestedProperty > 1)
                    }));
            translation.Select.ShouldBe(
                "[Entities].[ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1] AS ChildrenWhereNestedPropertyLessThan10CountNestedPropertyGreaterThan1");
        }

        [Fact]
        public void CanQueryWithSelectToNamedTypeWithDynamicallyNamedProperty()
        {
            var translation = Translate(Query<Entity>()
                    .Select(x => new ProjectedEntity
                    {
                        Property = x.Column<int>("SomeProperty")
                    }));

            translation.Select.ShouldBe("[Entities].[SomeProperty] AS Property");
        }

        [Fact]
        public void CanQueryWithSelectToNamedWithOtherName()
        {
            var translation = Translate(Query<Entity>().Select(x => new ProjectedEntity {StringProp = x.Field}));
            translation.Select.ShouldBe("[Entities].[Field] AS StringProp");
        }

        [Fact]
        public void CanQueryWithSelectTo()
        {
            var translation = Translate(Query<Entity>()
                    .Select(x => x.Column<int>("SomeProperty")));
            translation.Select.ShouldBe("[Entities].[SomeProperty]");
        }

        [Fact(Skip = "Feature tbd")]
        public void CanQueryWithTwoSelects()
        {
            var translation = Translate(Query<Entity>().Select(x => new {x.Field}).Select(x => x.Field));
            translation.Select.ShouldBe("[Entities].[Field[Entities].[ AS Field");
        }

        [Fact(Skip = "Feature tbd")]
        public void CanQueryWithTwoSelects2()
        {
            var translation = Translate(Query<Entity>().Select(x => new {Something = x.Column<string>("Field")}).Select(x => x.Something));
            translation.Select.ShouldBe("[Entities].[Field] AS Something");
        }

        [Fact]
        public void CanQueryWithSkipAndTake()
        {
            var translation = Translate(Query<Entity>().Skip(1).Take(1));
            translation.Skip.ShouldBe(1);
            translation.Take.ShouldBe(1);
        }

        [Fact]
        public void CanOrderBy()
        {
            var translation = Translate(Query<Entity>().OrderBy(x => x.Property));
            translation.OrderBy.ShouldBe("[Entities].[Property]");
        }

        [Fact]
        public void CanOrderByAndThenBy()
        {
            var translation = Translate(Query<Entity>().OrderBy(x => x.Property).ThenBy(x => x.StringProp));
            translation.OrderBy.ShouldBe("[Entities].[Property], [Entities].[StringProp]");
        }

        [Fact]
        public void CanOrderByDescending()
        {
            var translation = Translate(Query<Entity>().OrderByDescending(x => x.Property));
            translation.OrderBy.ShouldBe("[Entities].[Property] DESC");
        }

        [Fact]
        public void CanOrderByAndThenByDescending()
        {
            var translation = Translate(Query<Entity>().OrderBy(x => x.Property).ThenByDescending(x => x.StringProp));
            translation.OrderBy.ShouldBe("[Entities].[Property], [Entities].[StringProp] DESC");
        }

        [Fact]
        public void CanWriteGuid()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Id == Guid.Empty));
            translation.Where.ShouldBe("([Entities].[Id] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", Guid.Empty);
        }

        [Fact]
        public void CanQueryWhereWithBool()
        {
            var translation = Translate(Query<Entity>().Where(x => x.BoolProp));
            translation.Where.ShouldBe("([Entities].[BoolProp] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryWhereWithNotBool()
        {
            var translation = Translate(Query<Entity>().Where(x => !x.BoolProp));
            translation.Where.ShouldBe(" NOT ([Entities].[BoolProp] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void WhereWithNestedUnaryBool()
        {
            var translation = Translate(Query<Entity>().Where(x => true && x.BoolProp));
            translation.Where.ShouldBe("((1=1) AND ([Entities].[BoolProp] = @Value0))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryWhereWithBoolEquals()
        {
            var something = true;
            var translation = Translate(Query<Entity>().Where(x => x.BoolProp == something));
            translation.Where.ShouldBe("([Entities].[BoolProp] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", true);
        }

        [Fact]
        public void CanQueryWhereWithConstantMethodCall()
        {
            var translation = Translate(Query<Entity>().Where(x => x.BoolProp == WackyCustomEqualityCheck(1, 1)));
            translation.Where.ShouldBe("([Entities].[BoolProp] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", true);
        }

        bool WackyCustomEqualityCheck(int x, int y)
        {
            return x == y;
        }

        [Fact]
        public void CanQueryWhereWithConstantStaticMethodCall()
        {
            var translation = Translate(Query<Entity>().Where(x => StaticNoise(2) > 1));
            translation.Where.ShouldBe("(@Value0 > @Value1)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 2);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", 1);
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
            var translation = Translate(Query<Entity>().Where(x => x.Id.In(list)));
            translation.Where.ShouldBe("([Entities].[Id] IN (@Value0, @Value1))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", guid1);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithInArrayInitialized()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var translation = Translate(Query<Entity>().Where(x => x.Id.In(new[] {guid1, guid2})));
            translation.Where.ShouldBe("([Entities].[Id] IN (@Value0, @Value1))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", guid1);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithInArrayToArrayed()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var list = new[] {guid1, guid2};
            var translation = Translate(Query<Entity>().Where(x => x.Id.In(list.ToArray())));
            translation.Where.ShouldBe("([Entities].[Id] IN (@Value0, @Value1))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", guid1);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithInEmptyArray()
        {
            // ReSharper disable once RedundantExplicitParamsArrayCreation
            var translation = Translate(Query<Entity>().Where(x => x.Id.In(new Guid[0])));
            translation.Where.ShouldBe("(1<>1)");
            translation.ParametersByName.ShouldBeEmpty();
        }

        [Fact]
        public void CanQueryWhereWithNotInEmptyArray()
        {
            // ReSharper disable once RedundantExplicitParamsArrayCreation
            var translation = Translate(Query<Entity>().Where(x => !x.Id.In(new Guid[0])));
            translation.Where.ShouldBe(" NOT (1<>1)");
            translation.ParametersByName.ShouldBeEmpty();
        }

        [Fact]
        public void CanQueryWhereWithNotInArray()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var list = new[] {guid1, guid2};
            var translation = Translate(Query<Entity>().Where(x => !x.Id.In(list)));
            translation.Where.ShouldBe(" NOT ([Entities].[Id] IN (@Value0, @Value1))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", guid1);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithInOnUserDefinedColumn()
        {
            var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
            var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
            var list = new[] {guid1, guid2};
            var translation = Translate(Query<Entity>().Where(x => x.Column<Guid>("Id").In(list.ToArray())));
            translation.Where.ShouldBe("([Entities].[Id] IN (@Value0, @Value1))");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", guid1);
            translation.ParametersByName.ShouldContainKeyAndValue("@Value1", guid2);
        }

        [Fact]
        public void CanQueryWhereWithTrueBoolConstant()
        {
            var translation = Translate(Query<Entity>().Where(x => true));
            translation.Where.ShouldBe("(@Value0 = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryWhereWithFalseBoolConstant()
        {
            var translation = Translate(Query<Entity>().Where(x => false));
            translation.Where.ShouldBe("(@Value0 <> @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", 1);
        }

        [Fact]
        public void CanQueryOnIndexes()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Index<ExtIndex>().StringProp == "asger"));

            translation.Where.ShouldBe("([Entities].[StringProp] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", "asger");
        }

        [Fact(Skip = "Issue #30")]
        public void CanQueryEnums()
        {
            var translation = Translate(Query<Entity>().Where(x => x.Enum == Enumse.Second));

            translation.Where.ShouldBe("([Entities].[Enum] = @Value0)");
            translation.ParametersByName.ShouldContainKeyAndValue("@Value0", "Second");
        }

        [Fact]
        public void CanQueryOnUserDefinedColumnFromVariable()
        {
            var somecolumn = new Entity {StringProp = "SomeColumn"};
            var translation = Translate(Query<Entity>().OrderBy(x => x.Column<string>(somecolumn.StringProp.ToString())));

            translation.OrderBy.ShouldBe("[Entities].[SomeColumn]");
        }

        Query<T> Query<T>() where T : class
        {
            var documentStore = DocumentStore.ForTesting(TableMode.UseTempTables, connectionString);
            var documentDesign = documentStore.Configuration.GetOrCreateDesignFor(typeof (T));
            var session = new DocumentSession(documentStore);
            return new Query<T>(new QueryProvider(session, documentDesign));
        }

        static SqlStatementFragments Translate(IQueryable query)
        {
            var provider = (IHybridQueryProvider)query.Provider;
            return provider.GetQueryText(query.Expression);
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