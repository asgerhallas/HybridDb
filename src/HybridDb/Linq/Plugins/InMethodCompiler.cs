using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Compilers;
using ShinySwitch;

namespace HybridDb.Linq.Plugins
{
    public class InMethodCompiler : LinqPlugin
    {
        public override BonsaiExpression Compile(Expression exp, Compiler top, Compiler next) => Switch<BonsaiExpression>.On(exp)
            .Match<MethodCallExpression>(methodCall =>
            {
                if (methodCall.Method.DeclaringType != typeof(InMethodEx) || methodCall.Method.Name != nameof(InMethodEx.In)) return next(exp);

                var left = top(methodCall.Arguments[0]);
                var right = top(methodCall.Arguments[1]);

                var elementType = right.Type.GetElementTypeOfEnumerable();

                if (elementType == null) throw new ArgumentException($"{right.Type} is not a list.");

                return new In(left, right);
            })
            .Else(() => next(exp));

        public override BonsaiExpression PostProcess(BonsaiExpression exp, PostProcessor top, PostProcessor next) =>
            exp is In @in
                ? new In(top(@in.Needle), top(@in.Haystack))
                : next(exp);

        public override string Emit(BonsaiExpression exp, Emitter top, Emitter next) => Switch<string>.On(exp)
            .Match<In>(@in =>
            {
                var list = top(@in.Haystack);

                return list != string.Empty
                    ? $"{top(@in.Needle)} IN ({list})"
                    : "0 <> 0";
            })
            .Else(() => next(exp));

        public class In : BonsaiExpression
        {
            public In(BonsaiExpression needle, BonsaiExpression haystack) : base(typeof(bool))
            {
                Needle = needle;
                Haystack = haystack;
            }

            public BonsaiExpression Needle { get; }
            public BonsaiExpression Haystack { get; }
        }
    }

    public static class InMethodEx
    {
        public static bool In<T>(this T @this, params T[] ts) => ts.Contains(@this);
        public static bool In<T>(this T @this, IEnumerable<T> ts) => ts.Contains(@this);
    }
}