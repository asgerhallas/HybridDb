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

        [Fact()]
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
            TableFor<Entity>()["third"].Length.ShouldBe(1024);
        }

        public class Entity
        {
            public string String { get; set; }
            public List<string> Strings { get; set; }
            public int Number { get; set; }
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