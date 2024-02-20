using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HybridDb.Config;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentDesignerTests
    {
        readonly Configuration configuration;

        public DocumentDesignerTests()
        {
            configuration = new Configuration();
        }

        public Dictionary<string, Projection> ProjectionsFor<T>()
        {
            return configuration.GetDesignFor<T>().Projections;
        }

        public DocumentTable TableFor<T>()
        {
            return configuration.GetDesignFor<T>().Table;
        }

        [Fact]
        public void CanGetColumnNameFromSimpleProjection()
        {
            configuration.Document<Entity>().With(x => x.String);
            ProjectionsFor<Entity>().ShouldContainKey("String");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithMethod()
        {
            configuration.Document<Entity>().With(x => x.String.ToUpper());
            ProjectionsFor<Entity>().ShouldContainKey("StringToUpper");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithMethodAndArgument()
        {
            configuration.Document<Entity>().With(x => x.String.ToUpper(CultureInfo.InvariantCulture));
            ProjectionsFor<Entity>().ShouldContainKey("StringToUpperCultureInfoInvariantCulture");
        }

        [Fact(Skip = "until we support multikey indices")]
        public void CanGetColumnNameFromProjectionWithLambda()
        {
            configuration.Document<Entity>().With(x => x.Strings.Where(y => y == "Asger"));
            ProjectionsFor<Entity>().ShouldContainKey("StringsWhereEqualAsger");
        }

        [Fact(Skip = "until we support multikey indices")]
        public void CanGetColumnNameFromProjectionWithComplexLambda()
        {
            configuration.Document<Entity>().With(x => x.Strings.Where(y => y.PadLeft(2).Length > 10));
            ProjectionsFor<Entity>().ShouldContainKey("StringsWherePadLeft2LengthGreaterThan10");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithEnumFlags()
        {
            configuration.Document<Entity>().With(x => x.String.GetType().GetProperties(BindingFlags.Static | BindingFlags.Instance).Any());
            ProjectionsFor<Entity>().ShouldContainKey("StringGetTypeGetPropertiesInstanceStaticAny");
        }

        [Fact]
        public void CanOverrideProjectionsForSubtype()
        {
            DocumentDesigner<AbstractEntity> tempQualifier = configuration.Document<AbstractEntity>();
            Expression<Func<AbstractEntity, int>> projector = x => 1;
            DocumentDesigner<AbstractEntity> temp = tempQualifier.Column(projector, x1 => x1.Name("Number"), new Option[0])

            [Obsolete($"""
                The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                A sample rewrite could be from:
                    
                    .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                To:
                    
                    .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                """, true)];

            DocumentDesigner<DerivedEntity> tempQualifier1 = configuration.Document<DerivedEntity>();
            Expression<Func<DerivedEntity, int>> projector1 = x => 2;
            DocumentDesigner<DerivedEntity> temp1 = tempQualifier1.Column(projector1, x1 => x1.Name("Number"), new Option[0])

            [Obsolete($"""
                The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                A sample rewrite could be from:
                    
                    .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                To:
                    
                    .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                """, true)];

            ProjectionsFor<DerivedEntity>()["Number"].Projector(new DerivedEntity(), null).ShouldBe(2);
        }

        [Fact]
        public void ProjectionDirectlyFromEntity()
        {
            configuration.Document<Entity>().With(x => x.String);

            ProjectionsFor<Entity>()["String"].Projector(new Entity { String = "Asger" }, null).ShouldBe("Asger");
        }

        [Fact]
        public void ProjectionDirectlyFromEntityWithOtherClassAsExtension()
        {
            configuration.Document<OtherEntity>()
                .With(x => x.String)
                .Extend<Index>(e =>
                    e.With(x => x.Number, x => x.String.Length));

            ProjectionsFor<OtherEntity>()["String"].Projector(new OtherEntity { String = "Asger" }, null).ShouldBe("Asger");
            ProjectionsFor<OtherEntity>()["Number"].Projector(new OtherEntity { String = "Asger" }, null).ShouldBe(5);
        }

        [Fact]
        public void LastProjectionOfSameNameWins()
        {
            configuration.Document<OtherEntity>()
                .With(x => x.String)
                .Extend<Index>(e =>
                    e.With(x => x.String, x => x.String.Replace("a", "b")));

            ProjectionsFor<OtherEntity>()["String"].Projector(new OtherEntity { String = "asger" }, null).ShouldBe("bsger");
        }

        [Fact]
        public void AddsNonNullableColumnForNonNullableProjection()
        {
            configuration.Document<AbstractEntity>().With(x => x.Number);

            var sqlColumn = TableFor<AbstractEntity>()["Number"];
            sqlColumn.Type.ShouldBe(typeof(int));
            sqlColumn.Nullable.ShouldBe(false);
        }

        [Fact]
        public void AddNullableColumnForProjectionOnSubtypes()
        {
            configuration.Document<AbstractEntity>();
            configuration.Document<MoreDerivedEntity1>().With(x => x.Number);
            configuration.Document<MoreDerivedEntity2>();

            var sqlColumn = TableFor<AbstractEntity>()["Number"];
            sqlColumn.Type.ShouldBe(typeof(int));
            sqlColumn.Nullable.ShouldBe(true);
        }

        [Fact]
        public void FailsWhenTryingToOverrideProjectionWithNonCompatibleType()
        {
            configuration.Document<AbstractEntity>().With(x => x.Number);

            Should.Throw<InvalidOperationException>(() =>
                {
                    DocumentDesigner<MoreDerivedEntity1> tempQualifier = configuration.Document<MoreDerivedEntity1>();

                    Expression<Func<MoreDerivedEntity1, string>> projector = x => "OtherTypeThanBase";

                    return (DocumentDesigner<MoreDerivedEntity1>)tempQualifier.Column(projector, x1 => x1.Name("Number"), new Option[0])

                    [Obsolete($"""
                        The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                        A sample rewrite could be from:
                            
                            .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                        To:
                            
                            .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                        """, true)];
                })
                .Message.ShouldBe("Can not override projection for Number of type System.Int32 with a projection that returns System.String (on HybridDb.Tests.DocumentDesignerTests+MoreDerivedEntity1).");
        }

        [Fact]
        public void FailsWhenOverridingProjectionOnSiblingWithNonCompatibleType()
        {
            configuration.Document<AbstractEntity>();
            DocumentDesigner<MoreDerivedEntity1> tempQualifier = configuration.Document<MoreDerivedEntity1>();
            Expression<Func<MoreDerivedEntity1, int>> projector = x => 1;
            DocumentDesigner<MoreDerivedEntity1> temp = tempQualifier.Column(projector, x1 => x1.Name("Number"), new Option[0])

            [Obsolete($"""
                The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                A sample rewrite could be from:
                    
                    .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                To:
                    
                    .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                """, true)];

            Should.Throw<InvalidOperationException>(() =>
                {
                    DocumentDesigner<MoreDerivedEntity2> tempQualifier1 = configuration.Document<MoreDerivedEntity2>();

                    Expression<Func<MoreDerivedEntity2, string>> projector1 = x => "OtherTypeThanBase";

                    return (DocumentDesigner<MoreDerivedEntity2>)tempQualifier1.Column(projector1, x1 => x1.Name("Number"), new Option[0])

                    [Obsolete($"""
                        The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                        A sample rewrite could be from:
                            
                            .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                        To:
                            
                            .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                        """, true)];
                })
                .Message.ShouldBe("Can not override projection for Number of type System.Int32 with a projection that returns System.String (on HybridDb.Tests.DocumentDesignerTests+MoreDerivedEntity2).");
        }

        [Fact]
        public void FailsWhenOverridingProjectionOnSelfWithNonCompatibleType()
        {
            Should.Throw<InvalidOperationException>(() =>
            {
                DocumentDesigner<OtherEntity> tempQualifier = configuration.Document<OtherEntity>();

                Expression<Func<OtherEntity, int>> projector = x => 1;

                DocumentDesigner<OtherEntity> tempQualifier1 = tempQualifier.Column(projector, x1 => x1.Name("String"), new Option[0])

                [Obsolete($"""
                    The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                    A sample rewrite could be from:
                        
                        .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                    To:
                        
                        .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                    """, true)];

                Expression<Func<OtherEntity, string>> projector1 = x => "string";

                return (DocumentDesigner<OtherEntity>)tempQualifier1.Column(projector1, x2 => x2.Name("String"), new Option[0])

                [Obsolete($"""
                    The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                    A sample rewrite could be from:
                        
                        .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                    To:
                        
                        .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                    """, true)];
            });
        }

        [Fact]
        public void CanOverrideProjectionWithCompatibleType()
        {
            configuration.Document<AbstractEntity>().With(x => x.LongNumber);
            DocumentDesigner<MoreDerivedEntity1> tempQualifier = configuration.Document<MoreDerivedEntity1>();
            Expression<Func<MoreDerivedEntity1, int>> projector = x => x.Number;
            DocumentDesigner<MoreDerivedEntity1> temp = tempQualifier.Column(projector, x1 => x1.Name("LongNumber"), new Option[0])

            [Obsolete($"""
                The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                A sample rewrite could be from:
                    
                    .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                To:
                    
                    .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                """, true)];

            var sqlColumn = TableFor<AbstractEntity>()["LongNumber"];
            sqlColumn.Type.ShouldBe(typeof(long));
            sqlColumn.Nullable.ShouldBe(false);

            ProjectionsFor<AbstractEntity>()["LongNumber"].Projector(new MoreDerivedEntity1 { LongNumber = 1, Number = 2 }, null).ShouldBe(1);
            ProjectionsFor<MoreDerivedEntity1>()["LongNumber"].Projector(new MoreDerivedEntity1 { LongNumber = 1, Number = 2 }, null).ShouldBe(2);
        }

        [Fact]
        public void CanOverrideProjectionWithNullability()
        {
            configuration.Document<AbstractEntity>().With(x => x.Number);
            DocumentDesigner<MoreDerivedEntity1> tempQualifier = configuration.Document<MoreDerivedEntity1>();
            Expression<Func<MoreDerivedEntity1, int?>> projector = x => (int?)null;
            DocumentDesigner<MoreDerivedEntity1> temp = tempQualifier.Column(projector, x1 => x1.Name("Number"), new Option[0])

            [Obsolete($"""
                The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                A sample rewrite could be from:
                    
                    .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                To:
                    
                    .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                """, true)];

            var sqlColumn = TableFor<AbstractEntity>()["Number"];
            sqlColumn.Type.ShouldBe(typeof(int));
            sqlColumn.Nullable.ShouldBe(true);
        }

        [Fact]
        public void CanOverrideProjectionWithoutChangingNullability()
        {
            configuration.Document<AbstractEntity>().With(x => x.Number);
            configuration.Document<MoreDerivedEntity1>().With(x => x.Number);

            var sqlColumn = TableFor<AbstractEntity>()["Number"];
            sqlColumn.Type.ShouldBe(typeof(int));
            sqlColumn.Nullable.ShouldBe(false);
        }

        [Fact]
        public void MustSetLengthOnStringProjections()
        {
            DocumentDesigner<Entity> tempQualifier = configuration.Document<Entity>();
            Expression<Func<Entity, string>> projector = x => x.String;
            Option[] options = new[] { new MaxLength(255) };
            DocumentDesigner<Entity> tempQualifier1 = tempQualifier.Column(projector, x1 => x1.Name("first"), options)

            [Obsolete($"""
                The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                A sample rewrite could be from:
                    
                    .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                To:
                    
                    .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                """, true)];

            Expression<Func<Entity, string>> projector1 = x => x.String;
            Option[] options1 = new[] { new MaxLength() };
            DocumentDesigner<Entity> tempQualifier2 = tempQualifier1.Column(projector1, x2 => x2.Name("second"), options1)

            [Obsolete($"""
                The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                A sample rewrite could be from:
                    
                    .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                To:
                    
                    .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                """, true)];

            Expression<Func<Entity, string>> projector2 = x => x.String;
            DocumentDesigner<Entity> temp = tempQualifier2.Column(projector2, x1 => x1.Name("third"), new Option[0])

            [Obsolete($"""
                The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                A sample rewrite could be from:
                    
                    .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                To:
                    
                    .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                """, true)];

            TableFor<Entity>()["first"].Length.ShouldBe(255);
            TableFor<Entity>()["second"].Length.ShouldBe(-1);
            TableFor<Entity>()["third"].Length.ShouldBe(850);
        }

        [Fact]
        public void ConvertProjection()
        {
            configuration.Document<Entity>().With(x => x.String.Length);

            ProjectionsFor<Entity>()["String"].Projector(new Entity{ String = "Asger" }, null).ShouldBe(5);
        }

        [Fact]
        public void ConvertProjection_HandleNull()
        {
            configuration.Document<Entity>().With(x => x.String.Length);

            ProjectionsFor<Entity>()["String"].Projector(new Entity(), null).ShouldBe(null);
        }

        [Fact]
        public void NullCheckWithNonNullableValueTypeProjections()
        {
            DocumentDesigner<Entity> tempQualifier = configuration.Document<Entity>();
            Expression<Func<Entity, int>> projector = x => x.String.Length;
            DocumentDesigner<Entity> temp = tempQualifier.Column(projector, x1 => x1.Name("Test"), new Option[0])

            [Obsolete($"""
                The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

                A sample rewrite could be from:
                    
                    .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

                To:
                    
                    .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
                """, true)];

            ProjectionsFor<Entity>()["Test"].Projector(new Entity(), null).ShouldBe(null);
        }

        [Fact]
        public void DisallowSameDiscriminatorInSameTable()
        {
            configuration.Document<Namespace1.DerivedEntity>();
            Should.Throw<InvalidOperationException>(() => configuration.Document<Namespace2.DerivedEntity>())
                .Message.ShouldBe("Document 'DerivedEntity' has discriminator 'DerivedEntity' in table 'DerivedEntities'. This combination already exists, please select either another table or discriminator for the type.");
        }

        [Fact]
        public void DisallowSameDiscriminatorInSameTable_DifferentTypeName()
        {
            configuration.Document<Entity>(tablename:"t", discriminator:"u");
            Should.Throw<InvalidOperationException>(() => configuration.Document<OtherEntity>(tablename: "t", discriminator: "u"))
                .Message.ShouldBe("Document 'OtherEntity' has discriminator 'u' in table 't'. This combination already exists, please select either another table or discriminator for the type.");
        }

        [Fact]
        public void AllowSameDiscriminator_DifferentTables()
        {
            configuration.Document<Namespace1.DerivedEntity>(tablename:"t", discriminator:"u");
            configuration.Document<Namespace2.DerivedEntity>(tablename: "v", discriminator: "u");
        }


        [Fact]
        public void JsonProjection()
        {
            configuration.Document<Entity>().WithJson(x => x.Strings);

            ProjectionsFor<Entity>()["Strings"].Projector(new Entity { Strings = new List<string>{"hej","okay"} }, null).ShouldBe("[\"hej\",\"okay\"]");
        }

        [Fact]
        public void JsonProjection_Object()
        {
            configuration.Document<Entity<object>>().WithJson(x => x.Value);

            ProjectionsFor<Entity<object>>()["Value"].Projector(new Entity<object> { Value = new object()}, null).ShouldBe("{}");
        }

        [Fact]
        public void JsonProjection_ComplexType()
        {
            configuration.Document<Entity<OtherEntity>>().WithJson(x => x.Value);

            ProjectionsFor<Entity<OtherEntity>>()["Value"].Projector(new Entity<OtherEntity>{Value =  new OtherEntity{String = "ThisIsAString"}}, null)
                .ShouldBe("{\"String\":\"ThisIsAString\"}");
        }

        [Fact]
        public void ProjectionWithEnumTypes()
        {
            configuration.Document<Entity<EnumType>>().With(x => x.Value);

            ProjectionsFor<Entity<EnumType>>()["Value"].Projector(new Entity<EnumType> { Value = EnumType.Something }, null).ShouldBe(EnumType.Something);
        }

        [Fact]
        public void CorrectLengthOnJsonProperty()
        {
            configuration.Document<Entity<object>>().WithJson(x => x.Value);
            TableFor<Entity<object>>().Columns.Single(x => x.Name == "Value").Length.ShouldBe(-1);
        }

        [Fact]
        public void CorrectLengthOnJsonPropertyWhenOverwritingLength()
        {
            configuration.Document<Entity<object>>().WithJson(x => x.Value, new MaxLength(50));

            TableFor<Entity<object>>().Columns.Single(x => x.Name == "Value").Length.ShouldBe(50);
        }

        public class Entity
        {
            public string String { get; set; }
            public List<string> Strings { get; set; }
            public int Number { get; set; }
        }

        public class Entity<T>
        {
            public T Value { get; set; }
        }

        public class OtherEntity
        {
            public string String { get; set; }
        }

        public enum EnumType
        {
            Something = 1,
            SomethingElse = 2,
            SomethingCompletelyDifferent = 3
        }

        public abstract class AbstractEntity
        {
            public Guid Id { get; set; }
            public string Property { get; set; }
            public int Number { get; set; }
            public long LongNumber { get; set; }
        }

        public class DerivedEntity : AbstractEntity { }
        public class MoreDerivedEntity1 : DerivedEntity { }
        public class MoreDerivedEntity2 : DerivedEntity { }

        public class Index
        {
            public string String { get; set; }
            public int Number { get; set; }
        }
    }
}