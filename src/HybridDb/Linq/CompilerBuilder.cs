using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Compilers;
using HybridDb.Linq.Plugins;

namespace HybridDb.Linq
{
    public delegate BonsaiExpression Compiler(Expression exp);
    public delegate BonsaiExpression PostProcessor(BonsaiExpression exp);
    public delegate string Emitter(BonsaiExpression exp);

    public static class CompilerBuilder
    {
        public static LinqCompiler Compose(Func<Expression, Expression> preprocessor, params LinqPlugin[] plugins)
        {
            var (frontend, backend) = ComposeFrontendAndBackend(preprocessor, plugins);

            return new LinqCompiler(frontend, backend);
        }

        public static LinqCompiler Compose(params LinqPlugin[] plugins) => Compose(x => x, plugins);

        public static (Compiler compile, Emitter emit) ComposeFrontendAndBackend(Func<Expression, Expression> preprocessor, params LinqPlugin[] plugins)
        {
            var compiler = Compose(plugins.Select(plugin => Convert(plugin.Compile)));
            var postProcessor = Compose(plugins.Select(plugin => Convert(plugin.PostProcess)));
            var emitter = Compose(plugins.Select(plugin => Convert(plugin.Emit)));

            return (exp =>
            {
                var a = preprocessor(exp);
                var b = compiler(a);
                var c = postProcessor(b);
                return c;
            }, new Emitter(emitter));
        }

        public static Compiler Compose(params Func<Expression, Compiler, Compiler, BonsaiExpression>[] steps) =>
            new Compiler(Compose(steps.Select(Convert)));

        public static PostProcessor Compose(params Func<BonsaiExpression, PostProcessor, PostProcessor, BonsaiExpression>[] steps) =>
            new PostProcessor(Compose(steps.Select(Convert)));

        public static Emitter Compose(params Func<BonsaiExpression, Emitter, Emitter, string>[] steps) =>
            new Emitter(Compose(steps.Select(Convert)));

        static Func<TIn, TOut> Compose<TIn, TOut>(IEnumerable<Func<TIn, Func<TIn, TOut>, Func<TIn, TOut>, TOut>> steps)
        {
            var list = steps.ToList();
            return Compose(list, list);
        }

        static Func<TIn, TOut> Compose<TIn, TOut>(
            IReadOnlyList<Func<TIn, Func<TIn, TOut>, Func<TIn, TOut>, TOut>> top,
            IReadOnlyList<Func<TIn, Func<TIn, TOut>, Func<TIn, TOut>, TOut>> next)
        {
            return exp =>
            {
                if (next.Count == 0)
                {
                    throw new InvalidOperationException($"Could not compile {exp}.");
                }

                var head = next[0];
                var tail = next.Skip(1);

                return head(exp, Compose(top, top), Compose(top, tail.ToArray()));
            };
        }

        static Func<Expression, Func<Expression, BonsaiExpression>, Func<Expression, BonsaiExpression>, BonsaiExpression> Convert(
            Func<Expression, Compiler, Compiler, BonsaiExpression> compiler) => (a, b, c) => compiler(a, new Compiler(b), new Compiler(c));

        static Func<BonsaiExpression, Func<BonsaiExpression, BonsaiExpression>, Func<BonsaiExpression, BonsaiExpression>, BonsaiExpression> Convert(
            Func<BonsaiExpression, PostProcessor, PostProcessor, BonsaiExpression> compiler) => (a, b, c) => compiler(a, new PostProcessor(b), new PostProcessor(c));

        static Func<BonsaiExpression, Func<BonsaiExpression, string>, Func<BonsaiExpression, string>, string> Convert(
            Func<BonsaiExpression, Emitter, Emitter, string> compiler) => (a, b, c) => compiler(a, new Emitter(b), new Emitter(c));

        public static LinqCompiler DefaultCompiler => Compose(PreProcessors.All, 
            new InMethodCompiler(), 
            new NormalizeComparisons(), 
            new CastEnums(), 
            new ColumnNameCompiler(), 
            new LinqCompilerRoot());
    }
}