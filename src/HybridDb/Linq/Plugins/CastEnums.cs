using System.Linq;
using HybridDb.Linq.Bonsai;
using ShinySwitch;

namespace HybridDb.Linq.Plugins
{
    public class CastEnums : LinqPlugin
    {
        /// <summary>
        /// Expects PostCompilers.NormalizeComparisons to have been applied.
        /// </summary>
        public override BonsaiExpression PostProcess(BonsaiExpression exp, PostProcessor top, PostProcessor next) =>
            Switch<BonsaiExpression>.On(exp)
                .Match<Comparison>(x =>
                {
                    if (x.Left.Type.IsEnum && x.Right is Constant constant && x.Right.Type == typeof(int))
                    {
                        return new Comparison(x.Operator, top(x.Left), top(new Constant(constant.Value, x.Left.Type)));
                    }

                    return next(x);
                })
                .Match<List>(x =>
                {
                    if (x.ElementType.IsEnum)
                    {
                        return new List(
                            x.Values.Select(item => item is Constant constant
                                ? top(new Constant(constant.Value, x.ElementType))
                                : top(item)),
                            x.ElementType,
                            x.Type);
                    }

                    return next(x);
                })
                .Else(() => next(exp));
    }
}