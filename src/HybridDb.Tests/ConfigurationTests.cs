using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;
using Shouldly;

namespace HybridDb.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public void CanGetColumnNameFromSimpleProjection()
        {
            var name = Configuration.GetColumnNameByConventionFor((Expression<Func<Entity, object>>) (x => x.String));
            name.ShouldBe("String");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithMethod()
        {
            var name = Configuration.GetColumnNameByConventionFor((Expression<Func<Entity, object>>)(x => x.String.ToUpper()));
            name.ShouldBe("StringToUpper");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithMethodAndArgument()
        {
            var name = Configuration.GetColumnNameByConventionFor((Expression<Func<Entity, object>>)(x => x.String.ToUpper(CultureInfo.InvariantCulture)));
            name.ShouldBe("StringToUpperCultureInfoInvariantCulture");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithLambda()
        {
            var name = Configuration.GetColumnNameByConventionFor((Expression<Func<Entity, object>>)(x => x.Strings.Where(y => y == "Asger")));
            name.ShouldBe("StringsWhereEqualAsger");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithComplexLambda()
        {
            var name = Configuration.GetColumnNameByConventionFor((Expression<Func<Entity, object>>)(x => x.Strings.Where(y => y.PadLeft(2).Length > 10)));
            name.ShouldBe("StringsWherePadLeft2LengthGreaterThan10");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithEnumFlags()
        {
            var name = Configuration.GetColumnNameByConventionFor((Expression<Func<Entity, object>>)(x => 
                x.String.GetType().GetProperties(BindingFlags.Static | BindingFlags.Instance).Any()));
            
            name.ShouldBe("StringGetTypeGetPropertiesInstanceStaticAny");
        }

        [Fact]
        public void CanOverrideProjectionsForSubtype()
        {
            var configuration = new Configuration(null);

            configuration.Document<AbstractEntity>(null)
                .With("Number", x => 1);

            configuration.Document<DerivedEntity>(null)
                .With("Number", x => 2);

            var projection = configuration.GetDesignFor<DerivedEntity>().Projections["Number"];
            projection(new DerivedEntity()).ShouldBe(2);
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
        }

        public class DerivedEntity : AbstractEntity { }
        public class MoreDerivedEntity1 : DerivedEntity { }
        public class MoreDerivedEntity2 : DerivedEntity { }

        public class Index
        {
            public string String { get; set; }
            public int Number { get; set; }
        }

        public class IndexWithId
        {
            public Guid Id { get; set; }
            public string String { get; set; }
            public int Number { get; set; }
        }

        public class OtherIndex
        {
            public string String { get; set; }
            public int Number { get; set; }
        }

        public class WrongTypeIndex
        {
            public int String { get; set; }
        }

        public class BadMatchIndex
        {
            public int BadMatch { get; set; }
        }
    }
}