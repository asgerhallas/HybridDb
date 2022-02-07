using System;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Plugins;
using ShinySwitch;

namespace HybridDb.Linq.Compilers
{
    public class QueryCompiler : LinqPlugin
    {
        record QueryMethodCall(BonsaiExpression Method, Expression Next);

        public BonsaiExpression CompileQuery(Expression exp, Compiler top, Compiler next)
        {

            BonsaiExpression select = null;


            while (Compile(exp, top, next) is {Next: not null} result)
            {
                if (result.Method is Select)
            }


        }

        public QueryMethodCall Compile(Expression exp, Compiler top, Compiler next) => Switch<BonsaiExpression>.On(exp)
            .Match<MethodCallExpression>(methodCall => 
                Switch<QueryMethodCall>.On((methodCall.Method.DeclaringType, methodCall.Method.Name))
                    .Match((typeof(IQueryable), nameof(Queryable.Select)), _ => CompileSelect(methodCall, top, next))
                    .Else(() => next(exp)))
            .Else(() => throw new HybridDbException($"'{exp}' is not a query."));

        QueryMethodCall CompileSelect(MethodCallExpression methodCall, Compiler top, Compiler next)
        {
            var left = methodCall.Arguments[0];
            var right = top(methodCall.Arguments[1]);

            var elementType = right.Type.GetElementTypeOfEnumerable();

            if (elementType == null) throw new ArgumentException($"{right.Type} is not a list.");

            return new QueryMethodCall(null, left);

            //Select = SelectParser.Translate(expression.Arguments[1]);
            //// if it changes the return type make it known that this is a projection and should not be tracked in session
            //var inType = expression.Arguments[0].Type.GetGenericArguments()[0];
            //var outType = expression.Method.ReturnType.GetGenericArguments()[0];
            //ProjectAs = inType != outType ? outType : null;

        }

        //public override BonsaiExpression PostProcess(BonsaiExpression exp, PostProcessor top, PostProcessor next) =>
        //    exp is In @in
        //        ? new In(top(@in.Needle), top(@in.Haystack))
        //        : next(exp);

        //public override string Emit(BonsaiExpression exp, Emitter top, Emitter next) => Switch<string>.On(exp)
        //    .Match<In>(@in =>
        //    {
        //        var list = top(@in.Haystack);

        //        return list != string.Empty
        //            ? $"{top(@in.Needle)} IN ({list})"
        //            : "0 <> 0";
        //    })
        //    .Else(() => next(exp));

        public class Select : BonsaiExpression
        {
            public Select(Type returnType) : base(typeof(bool))
            {
            }

        }
    }
}