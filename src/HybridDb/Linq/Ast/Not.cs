using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Ast
{
    public class Not : SqlExpression
    {
        public Not(Predicate operand)
        {
            Operand = operand;
        }

        public Predicate Operand { get; }
    }
}