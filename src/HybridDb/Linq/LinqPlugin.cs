using System.Linq.Expressions;
using HybridDb.Linq.Bonsai;

namespace HybridDb.Linq
{
    public abstract class LinqPlugin
    {  
        public virtual BonsaiExpression Compile(Expression exp, Compiler top, Compiler next) => next(exp);
        public virtual BonsaiExpression PostProcess(BonsaiExpression exp, PostProcessor top, PostProcessor next) => next(exp);
        public virtual string Emit(BonsaiExpression exp, Emitter top, Emitter next) => next(exp);
    }
}