namespace HybridDb.Linq2.Ast
{
    public class Comparison : Predicate, IBinaryOperator
    {
        public Comparison(ComparisonOperator @operator, SqlExpression left, SqlExpression right)
        {
            Operator = @operator;
            Left = left;
            Right = right;
        }

        public ComparisonOperator Operator { get; }
        public SqlExpression Left { get; }
        public SqlExpression Right { get; }
    }
}