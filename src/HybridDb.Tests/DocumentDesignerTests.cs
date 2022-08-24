using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            configuration.Document<AbstractEntity>()
                .With("Number", x => 1);

            configuration.Document<DerivedEntity>()
                .With("Number", x => 2);

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

            Should.Throw<InvalidOperationException>(() => configuration.Document<MoreDerivedEntity1>().With("Number", x => "OtherTypeThanBase"))
                .Message.ShouldBe("Can not override projection for Number of type System.Int32 with a projection that returns System.String (on HybridDb.Tests.DocumentDesignerTests+MoreDerivedEntity1).");
        }

        [Fact]
        public void FailsWhenOverridingProjectionOnSiblingWithNonCompatibleType()
        {
            configuration.Document<AbstractEntity>();
            configuration.Document<MoreDerivedEntity1>().With("Number", x => 1);

            Should.Throw<InvalidOperationException>(() => configuration.Document<MoreDerivedEntity2>().With("Number", x => "OtherTypeThanBase"))
                .Message.ShouldBe("Can not override projection for Number of type System.Int32 with a projection that returns System.String (on HybridDb.Tests.DocumentDesignerTests+MoreDerivedEntity2).");
        }

        [Fact]
        public void FailsWhenOverridingProjectionOnSelfWithNonCompatibleType()
        {
            Should.Throw<InvalidOperationException>(() =>
                configuration.Document<OtherEntity>()
                    .With("String", x => 1)
                    .With("String", x => "string"));
        }

        [Fact]
        public void CanOverrideProjectionWithCompatibleType()
        {
            configuration.Document<AbstractEntity>().With(x => x.LongNumber);
            configuration.Document<MoreDerivedEntity1>().With("LongNumber", x => x.Number);

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
            configuration.Document<MoreDerivedEntity1>().With("Number", x => (int?)null);

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
            configuration.Document<Entity>()
                .With("first", x => x.String, new MaxLength(255))
                .With("second", x => x.String, new MaxLength())
                .With("third", x => x.String);

            TableFor<Entity>()["first"].Length.ShouldBe(255);
            TableFor<Entity>()["second"].Length.ShouldBe(-1);
            TableFor<Entity>()["third"].Length.ShouldBe(850);
        }

        [Fact]
        public void ConvertProjection()
        {
            configuration.Document<Entity>().With(x => x.String, x => x.Length);

            ProjectionsFor<Entity>()["String"].Projector(new Entity{ String = "Asger" }, null).ShouldBe(5);
        }

        [Fact]
        public void ConvertProjection_HandleNull()
        {
            configuration.Document<Entity>().With(x => x.String, x => x.Length);

            ProjectionsFor<Entity>()["String"].Projector(new Entity(), null).ShouldBe(null);
        }

        [Fact]
        public void NullCheckWithNonNullableValueTypeProjections()
        {
            configuration.Document<Entity>().With("Test", x => x.String.Length);

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
            configuration.Document<Entity>().With(x => x.Strings);

            ProjectionsFor<Entity>()["Strings"].Projector(new Entity { Strings = new List<string>{"hej","okay"} }, null).ShouldBe("[\"hej\",\"okay\"]");
        }

        [Fact]
        public void JsonProjection_Object()
        {
            configuration.Document<Entity<object>>().With(x => x.Value);

            ProjectionsFor<Entity<object>>()["Value"].Projector(new Entity<object> { Value = new object()}, null).ShouldBe("{}");
        }

        [Fact]
        public void JsonProjection_ComplexType()
        {
            configuration.Document<Entity<OtherEntity>>().With(x => x.Value);

            ProjectionsFor<Entity<OtherEntity>>()["Value"].Projector(new Entity<OtherEntity>{Value =  new OtherEntity{String = "ThisIsAString"}}, null)
                .ShouldBe(String.Format("{0}\"{1}\":\"{2}\"{3}", "{", "String" , "ThisIsAString", "}" ));
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