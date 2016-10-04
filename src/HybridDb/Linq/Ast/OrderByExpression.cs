using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Ast
{
    public class OrderByExpression : SqlExpression
    {
        public OrderByExpression(SqlExpression expression, Directions direction)
        {
            Direction = direction;
            Expression = expression;
        }

        public SqlExpression Expression { get; }
        public Directions Direction { get; }

        public enum Directions
        {
            Ascending,
            Descending
        }
    }
}