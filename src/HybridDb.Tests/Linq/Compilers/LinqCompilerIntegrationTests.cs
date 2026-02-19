using HybridDb.Linq;
using HybridDb.Linq.Plugins;
using ShouldBeLike;
using Xunit;

namespace HybridDb.Tests.Linq.Compilers
{
    public class LinqCompilerIntegrationTests : CompilerTests
    {
        readonly LinqCompiler compiler = CompilerBuilder.DefaultCompiler;

        [Fact]
        public void EnumComparisons()
        {
            compiler.Compile(F<A>(x => x.EnumProperty == MyEnum.Y))
                .ShouldBeLike("EnumProperty = 'Y'");
        }

        [Fact]
        public void EnumLists()
        {
            compiler.Compile(F<A>(x => x.EnumProperty.In(MyEnum.X, MyEnum.Y, MyEnum.Z)))
                .ShouldBeLike("EnumProperty IN ('X', 'Y', 'Z')");
        }

        [Fact]
        public void Bug1()
        {
            compiler.Compile(F<A>(x => x.EnumProperty == MyEnum.X && !x.BoolProperty))
                .ShouldBeLike("EnumProperty = 'X' AND NOT (BoolProperty = 1)");
        }

        public class A
        {
            public MyEnum EnumProperty { get; set; }
            public bool BoolProperty { get; set; }
        }

        public enum MyEnum
        {
            X,
            Y,
            Z
        }
    }
}