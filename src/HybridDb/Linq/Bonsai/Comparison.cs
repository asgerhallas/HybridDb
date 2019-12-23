namespace HybridDb.Linq.Bonsai
{
    public class Comparison : BonsaiExpression
    {
        public Comparison(ComparisonOperator @operator, BonsaiExpression left, BonsaiExpression right) : base(typeof(bool))
        {
            Operator = @operator;
            Left = AssertNotNull(left);
            Right = AssertNotNull(right);
        }

        public ComparisonOperator Operator { get; }
        public BonsaiExpression Left { get; }
        public BonsaiExpression Right { get; }
    }
}