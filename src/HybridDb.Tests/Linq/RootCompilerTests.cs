using HybridDb.Linq;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Compilers;
using HybridDb.Tests.Linq.Compilers;
using ShouldBeLike;
using Xunit;

namespace HybridDb.Tests.Linq
{
    public class RootCompilerTests : CompilerTests
    {
        readonly Compiler compile = CompilerBuilder.Compose(RootCompiler.Compile);

        [Fact]
        public void ComparisonEqual() =>
            compile(F<string>(x => x.Length == 10))
                .ShouldBeLike(new Comparison(ComparisonOperator.Equal,
                    new Column("Length", false, typeof(int)),
                    new Constant(10, typeof(int))));

        [Fact]
        public void ComparisonNotEqual() =>
            compile(F<string>(x => x.Length != 10))
                .ShouldBeLike(new Comparison(ComparisonOperator.NotEqual,
                    new Column("Length", false, typeof(int)),
                    new Constant(10, typeof(int))));

        [Fact]
        public void ComparisonLessThan() =>
            compile(F<string>(x => x.Length < 10))
                .ShouldBeLike(new Comparison(ComparisonOperator.LessThan,
                    new Column("Length", false, typeof(int)),
                    new Constant(10, typeof(int))));

        [Fact]
        public void ComparisonLessOrEqualThan() =>
            compile(F<string>(x => x.Length <= 10))
                .ShouldBeLike(new Comparison(ComparisonOperator.LessThanOrEqualTo,
                    new Column("Length", false, typeof(int)),
                    new Constant(10, typeof(int))));

        [Fact]
        public void ComparisonGreaterThan() =>
            compile(F<string>(x => x.Length > 10))
                .ShouldBeLike(new Comparison(ComparisonOperator.GreaterThan,
                    new Column("Length", false, typeof(int)),
                    new Constant(10, typeof(int))));

        [Fact]
        public void ComparisonGreaterOrEqualThan() =>
            compile(F<string>(x => x.Length >= 10))
                .ShouldBeLike(new Comparison(ComparisonOperator.GreaterThanOrEqualTo,
                    new Column("Length", false, typeof(int)),
                    new Constant(10, typeof(int))));

        [Fact]
        public void BinaryLogicAndAlso() =>
            compile(F<string>(x => x.Length > 5 && x.Length < 10))
                .ShouldBeLike(new BinaryLogic(BinaryLogicOperator.AndAlso,
                    new Comparison(ComparisonOperator.GreaterThan,
                        new Column("Length", false, typeof(int)),
                        new Constant(5, typeof(int))),
                    new Comparison(ComparisonOperator.LessThan,
                        new Column("Length", false, typeof(int)),
                        new Constant(10, typeof(int)))));

        [Fact]
        public void BinaryLogicOrElse() =>
            compile(F<string>(x => x.Length > 5 || x.Length < 10))
                .ShouldBeLike(new BinaryLogic(BinaryLogicOperator.OrElse,
                    new Comparison(ComparisonOperator.GreaterThan,
                        new Column("Length", false, typeof(int)),
                        new Constant(5, typeof(int))),
                    new Comparison(ComparisonOperator.LessThan,
                        new Column("Length", false, typeof(int)),
                        new Constant(10, typeof(int)))));

        [Fact]
        public void Not() =>
            compile(F<A>(x => !x.BoolProperty))
                .ShouldBeLike(new UnaryLogic(UnaryLogicOperator.Not,
                    new Column("BoolProperty", false, typeof(bool))));

        [Fact]
        public void Columns() =>
            compile(F<A>(x => x.BoolProperty))
                .ShouldBeLike(new Column("BoolProperty", false, typeof(bool)));

        [Fact]
        public void ConstantNull() => 
            compile(F<A>(x => null))
                .ShouldBeLike(new Constant(null, typeof(object)));

        public class A
        {
            public bool BoolProperty { get; set; }
            public MyEnum EnumProperty { get; set; }
            public A Self { get; set; }
        }

        public enum MyEnum
        {
            X,
            Y,
            Z
        }
    }
}