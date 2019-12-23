using HybridDb.Linq.Bonsai;
using ShinySwitch;

namespace HybridDb.Linq.Compilers
{
    public class NormalizeComparisons : LinqPlugin
    {
        public override BonsaiExpression PostProcess(BonsaiExpression exp, PostProcessor top, PostProcessor next)
        {
            return Switch<BonsaiExpression>.On(exp)
                .Match<Comparison>(x =>
                {
                    if (x.Left is Column) return next(x);

                    if (x.Right is Column) return new Comparison(x.Operator, top(x.Right), top(x.Left));

                    return next(x);
                })
                .Else(() => next(exp));
        }
    }
}