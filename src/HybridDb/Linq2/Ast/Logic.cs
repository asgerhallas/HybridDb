namespace HybridDb.Linq2.Ast
{
    public class Logic : Predicate, IBinaryOperator
    {
        public Logic(LogicOperator @operator, Predicate left, Predicate right)
        {
            Operator = @operator;
            Left = left;
            Right = right;
        }

        public LogicOperator Operator { get; }
        public Predicate Left { get; }
        public Predicate Right { get; }

        SqlExpression IBinaryOperator.Left => Left;
        SqlExpression IBinaryOperator.Right => Right;
    }
}