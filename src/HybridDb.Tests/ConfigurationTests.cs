using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HybridDb.Schema;
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
        public void CanGetBestMatchingIndex()
        {
            var configuration = new Configuration(null);
            configuration.Document<Entity>(null).Index<Index>();
            var indexTable = configuration.TryGetBestMatchingIndexTableFor<Entity>();
            indexTable.Name.ShouldBe("Index");
        }

        [Fact]
        public void CanGetBestMatchingIndexWhenMultipleTypesSameIndex()
        {
            var configuration = new Configuration(null);

            configuration.Document<MoreDerivedEntity1>(null).Index<Index>();
            configuration.Document<MoreDerivedEntity2>(null).Index<Index>();

            var indexTable = configuration.TryGetBestMatchingIndexTableFor<MoreDerivedEntity1>();
            indexTable.Name.ShouldBe("Index");
        }

        [Fact]
        public void CanGetBestMatchingIndexByBaseType()
        {
            var configuration = new Configuration(null);

            configuration.Document<MoreDerivedEntity1>(null).Index<Index>();
            configuration.Document<MoreDerivedEntity2>(null).Index<Index>();

            var indexTable = configuration.TryGetBestMatchingIndexTableFor<DerivedEntity>();
            indexTable.Name.ShouldBe("Index");
        }

        [Fact]
        public void DoesNotFindIndexWhenOneDerivedTypeDoesNotHaveIndex()
        {
            var configuration = new Configuration(null);

            configuration.Document<DerivedEntity>(null);
            configuration.Document<MoreDerivedEntity1>(null).Index<Index>();
            configuration.Document<MoreDerivedEntity2>(null).Index<Index>();

            var indexTable = configuration.TryGetBestMatchingIndexTableFor<AbstractEntity>();
            indexTable.ShouldBe(null);
        }

        [Fact]
        public void DoesNotFindIndexWhenOneDerivedTypeHasOtherIndex()
        {
            var configuration = new Configuration(null);

            configuration.Document<DerivedEntity>(null).Index<OtherIndex>();
            configuration.Document<MoreDerivedEntity1>(null).Index<Index>();
            configuration.Document<MoreDerivedEntity2>(null).Index<Index>();

            var indexTable = configuration.TryGetBestMatchingIndexTableFor<AbstractEntity>();
            indexTable.ShouldBe(null);
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

        public class OtherIndex
        {
            public string String { get; set; }
            public int Number { get; set; }
        }
    }
}