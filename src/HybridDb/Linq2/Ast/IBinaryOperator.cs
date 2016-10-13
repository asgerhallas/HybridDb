namespace HybridDb.Linq2.Ast
{
    public interface IBinaryOperator
    {
        SqlExpression Left { get; }
        SqlExpression Right { get; }
    }
}