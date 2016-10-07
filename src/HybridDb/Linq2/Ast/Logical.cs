using System;

namespace HybridDb.Linq2.Ast
{
    public interface IBinaryOperator
    {
        SqlExpression Left { get; }
        SqlExpression Right { get; }
    }

    public class Logical : Predicate, IBinaryOperator
    {
        public Logical(LogicalOperator @operator, Predicate left, Predicate right)
        {
            Operator = @operator;
            Left = left;
            Right = right;
        }

        public LogicalOperator Operator { get; }
        public Predicate Left { get; }
        public Predicate Right { get; }

        SqlExpression IBinaryOperator.Left => Left;
        SqlExpression IBinaryOperator.Right => Right;
    }
}