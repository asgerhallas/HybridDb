using System.Collections.Generic;
using HybridDb.Linq;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Plugins;
using HybridDb.Tests.Linq.Compilers;
using ShouldBeLike;
using Xunit;

namespace HybridDb.Tests.Linq.Plugins
{
    public class CastEnumsTests : CompilerTests
    {
        readonly PostProcessor compile;

        public CastEnumsTests() => compile = CompilerBuilder.Compose(new CastEnums().PostProcess, new LinqCompilerRoot().PostProcess);

        [Fact]
        public void CastIntsToEnumsInEnumComparisons()
        {
            compile(
                new Comparison(ComparisonOperator.Equal,
                    new Column("X", false, typeof(MyEnum)),
                    new Constant(1, typeof(int)))
            ).ShouldBeLike(
                new Comparison(ComparisonOperator.Equal,
                    new Column("X", false, typeof(MyEnum)),
                    new Constant(1, typeof(MyEnum))));
        }

        [Fact]
        public void DoNotCastIntsToEnumsInOtherComparisons()
        {
            compile(
                new Comparison(ComparisonOperator.Equal,
                    new Column("X", false, typeof(int)),
                    new Constant(1, typeof(int)))
            ).ShouldBeLike(
                new Comparison(ComparisonOperator.Equal,
                    new Column("X", false, typeof(int)),
                    new Constant(1, typeof(int))));
        }

        [Fact]
        public void CastIntsToEnumsInEnumLists() => 
            compile(
                new List(Helpers.ListOf(new Constant(1, typeof(int)), new Constant(1, typeof(int))), typeof(MyEnum), typeof(IEnumerable<MyEnum>)))
            .ShouldBeLike(
                new List(Helpers.ListOf(new Constant(1, typeof(MyEnum)), new Constant(1, typeof(MyEnum))), typeof(MyEnum), typeof(IEnumerable<MyEnum>)));

        public enum MyEnum
        {
            X,
            Y
        }
    }
}