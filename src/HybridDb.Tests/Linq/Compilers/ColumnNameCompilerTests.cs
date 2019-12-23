using HybridDb.Linq;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Compilers;
using ShouldBeLike;
using Xunit;

namespace HybridDb.Tests.Linq.Compilers
{
    public class ColumnNameCompilerTests : CompilerTests
    {
        readonly Compiler compile;

        public ColumnNameCompilerTests() => (compile, _) = CompilerBuilder
            .ComposeFrontendAndBackend(PreProcessors.All, new ColumnNameCompiler(), new LinqCompilerRoot());

        [Fact]
        public void Column() =>
            compile(F<A>(x => x.Property))
                .ShouldBeLike(new Column("Property", false, typeof(string)));

        [Fact]
        public void ColumnsDeeper() =>
            compile(F<A>(x => x.Self.Property))
                .ShouldBeLike(new Column("SelfProperty", false, typeof(string)));

        [Fact]
        public void Bug_IsMetaData()
        {
            compile(F<A>((view, x) => view.Key))
                .ShouldBeLike(new Column("Key", true, typeof(string)));
        }

        public class A
        {
            public string Property { get; set; }
            public A Self { get; set; }
        }
    }
}