namespace HybridDb.Linq.Bonsai
{
    public class BinaryLogic : BonsaiExpression
    {
        public BinaryLogic(BinaryLogicOperator @operator, BonsaiExpression left, BonsaiExpression right) : base(typeof(bool))
        {
            Operator = @operator;
            Left = AssertNotNull(left);
            Right = AssertNotNull(right);
        }

        public BinaryLogicOperator Operator { get; }
        public BonsaiExpression Left { get; }
        public BonsaiExpression Right { get; }
    }
}