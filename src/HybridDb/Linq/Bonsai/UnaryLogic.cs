namespace HybridDb.Linq.Bonsai
{
    public class UnaryLogic : BonsaiExpression
    {
        public UnaryLogic(UnaryLogicOperator @operator, BonsaiExpression expression) : base(typeof(bool))
        {
            Operator = @operator;
            Expression = expression;
        }

        public UnaryLogicOperator Operator { get; }
        public BonsaiExpression Expression { get; }
    }
}