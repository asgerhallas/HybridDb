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
        readonly Configuration conf;

        public ConfigurationTests()
        {
            conf = new Configuration();
        }

        [Fact]
        public void CanGetColumnNameFromSimpleProjection()
        {
            var name = conf.GetColumnNameFor((Expression<Func<Entity, object>>) (x => x.String));
            name.ShouldBe("String");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithMethod()
        {
            var name = conf.GetColumnNameFor((Expression<Func<Entity, object>>) (x => x.String.ToUpper()));
            name.ShouldBe("StringToUpper");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithMethodAndArgument()
        {
            var name = conf.GetColumnNameFor((Expression<Func<Entity, object>>) (x => x.String.ToUpper(CultureInfo.InvariantCulture)));
            name.ShouldBe("StringToUpperCultureInfoInvariantCulture");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithLambda()
        {
            var name = conf.GetColumnNameFor((Expression<Func<Entity, object>>) (x => x.Strings.Where(y => y == "Asger")));
            name.ShouldBe("StringsWhereEqualAsger");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithComplexLambda()
        {
            var name = conf.GetColumnNameFor((Expression<Func<Entity, object>>) (x => x.Strings.Where(y => y.PadLeft(2).Length > 10)));
            name.ShouldBe("StringsWherePadLeft2LengthGreaterThan10");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithEnumFlags()
        {
            var name = conf.GetColumnNameFor((Expression<Func<Entity, object>>) (x => 
                x.String.GetType().GetProperties(BindingFlags.Static | BindingFlags.Instance).Any()));
            
            name.ShouldBe("StringGetTypeGetPropertiesInstanceStaticAny");
        }

        public class Entity
        {
            public string String { get; set; }
            public List<string> Strings { get; set; }
        }
    }
}