namespace HybridDb.Linq2.Ast
{
    public class Comparison : Predicate
    {
        public Comparison(ComparisonOperator @operator, Expression left, Expression right)
        {
            Operator = @operator;
            Left = left;
            Right = right;
        }

        public ComparisonOperator Operator { get; }
        public Expression Left { get; }
        public Expression Right { get; }
    }
}