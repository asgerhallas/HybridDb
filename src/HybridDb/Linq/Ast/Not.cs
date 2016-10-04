using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Ast
{
    public class Not : SqlExpression
    {
        public Not(SqlExpression operand)
        {
            Operand = operand;
        }

        public SqlExpression Operand { get; }
    }
}