using HybridDb.Linq2.Ast;

namespace HybridDb.Linq2.Emitter
{
    public static class EmitHelper
    {
        public static EmitResult Emit(this EmitResult result, SqlExpression expression)
        {
            return SqlStatementEmitter.Emit(result, expression);
        }
    }
}