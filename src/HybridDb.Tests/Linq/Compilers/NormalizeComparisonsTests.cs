using HybridDb.Linq;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Compilers;
using ShouldBeLike;
using Xunit;

namespace HybridDb.Tests.Linq.Compilers
{
    public class NormalizeComparisonsTests
    {
        readonly PostProcessor compile;

        public NormalizeComparisonsTests() => compile = CompilerBuilder.Compose(new NormalizeComparisons().PostProcess, new LinqCompilerRoot().PostProcess);

        [Fact]
        public void RightColumnIsMovedLeft() => compile(
            new Comparison(ComparisonOperator.Equal,
                new Constant(1, typeof(int)),
                new Column("X", false, typeof(int)))
        ).ShouldBeLike(
            new Comparison(ComparisonOperator.Equal,
                new Column("X", false, typeof(int)),
                new Constant(1, typeof(int))));

        [Fact]
        public void SkipLeftColumn() => compile(
            new Comparison(ComparisonOperator.Equal,
                new Column("X", false, typeof(int)),
                new Constant(1, typeof(int)))
        ).ShouldBeLike(
            new Comparison(ComparisonOperator.Equal,
                new Column("X", false, typeof(int)),
                new Constant(1, typeof(int))));

        [Fact]
        public void SkipTwoColumns() => compile(
            new Comparison(ComparisonOperator.Equal,
                new Column("Y", false, typeof(int)),
                new Column("X", false, typeof(int)))
        ).ShouldBeLike(
            new Comparison(ComparisonOperator.Equal,
                new Column("Y", false, typeof(int)),
                new Column("X", false, typeof(int))));

        [Fact]
        public void SkipTwoValues() => compile(
            new Comparison(ComparisonOperator.Equal,
                new Constant(1, typeof(int)),
                new Constant(2, typeof(int)))
        ).ShouldBeLike(
            new Comparison(ComparisonOperator.Equal,
                new Constant(1, typeof(int)),
                new Constant(2, typeof(int))));
    }
}