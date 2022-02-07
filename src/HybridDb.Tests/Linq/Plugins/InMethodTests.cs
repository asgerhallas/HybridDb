using System.Collections.Generic;
using HybridDb.Linq;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Compilers;
using HybridDb.Linq.Plugins;
using HybridDb.Tests.Linq.Compilers;
using ShouldBeLike;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Linq.Plugins
{
    public class InMethodTests : CompilerTests
    {
        readonly Compiler compile;
        readonly Emitter emit;

        public InMethodTests() => (compile, emit) = CompilerBuilder.ComposeFrontendAndBackend(PreProcessors.All, new InMethodCompiler(), new LinqCompilerRoot());

        [Fact]
        public void Compile_InConstantArray()
        {
            var needles = new[] {"Hello", "World"};

            compile(F<A>(x => x.Value.In(needles)))
                .ShouldBeLike(new InMethodCompiler.In(
                    new Column("Value", false, typeof(string)),
                    new List(Helpers.ListOf(
                            new Constant("Hello", typeof(string)),
                            new Constant("World", typeof(string))),
                        typeof(string), typeof(string[]))));
        }

        [Fact]
        public void Compile_InConstantParamsArray() =>
            compile(F<A>(x => x.Value.In("Hello", "World")))
                .ShouldBeLike(new InMethodCompiler.In(
                    new Column("Value", false, typeof(string)),
                    new List(Helpers.ListOf(
                            new Constant("Hello", typeof(string)),
                            new Constant("World", typeof(string))),
                        typeof(string), typeof(string[]))));

        [Fact]
        public void Compile_InConstantEnumerable()
        {
            IEnumerable<string> MakeNeedles()
            {
                yield return "Hello";
                yield return "World";
            }

            var needles = MakeNeedles();

            compile(F<A>(x => x.Value.In(needles)))
                .ShouldBeLike(new InMethodCompiler.In(
                    new Column("Value", false, typeof(string)),
                    new List(Helpers.ListOf(
                            new Constant("Hello", typeof(string)),
                            new Constant("World", typeof(string))),
                        typeof(string), typeof(IEnumerable<string>))));
        }

        [Fact]
        public void EmitSql_Constants() =>
            emit(new InMethodCompiler.In(
                new Column("Id", false, typeof(string)),
                new List(Helpers.ListOf(
                        new Constant("Hello", typeof(string)),
                        new Constant("World", typeof(string))),
                    typeof(string), typeof(string[])))
            ).ShouldBe("Id IN ('Hello', 'World')");

        [Fact]
        public void EmitSql_Nothing() =>
            emit(new InMethodCompiler.In(new Column("Id", false, typeof(string)), new List(new BonsaiExpression[0], typeof(string), typeof(string[]))))
                .ShouldBe("0 <> 0");

        public class A
        {
            public string Value { get; set; }
        }
    }
}