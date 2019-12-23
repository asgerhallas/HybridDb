using System.Linq.Expressions;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Compilers;

namespace HybridDb.Linq
{
    public class LinqCompilerRoot : LinqPlugin
    {
        public override BonsaiExpression Compile(Expression exp, Compiler top, Compiler next) => RootCompiler.Compile(exp, top, next);

        public override BonsaiExpression PostProcess(BonsaiExpression exp, PostProcessor top, PostProcessor next) => RootCompiler.PostProcess(exp, top, next);

        public override string Emit(BonsaiExpression exp, Emitter top, Emitter next) => SqlEmitter.Emit(exp, top, next);
    }
}