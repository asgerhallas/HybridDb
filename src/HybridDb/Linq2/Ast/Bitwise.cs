namespace HybridDb.Linq2.Ast
{
    public class Bitwise : SqlExpression, IBinaryOperator
    {
        public Bitwise(BitwiseOperator @operator, SqlExpression left, SqlExpression right)
        {
            Operator = @operator;
            Left = left;
            Right = right;
        }

        public BitwiseOperator Operator { get; }
        public SqlExpression Left { get; }
        public SqlExpression Right { get; }
    }
}