namespace HybridDb.Linq.Ast
{
    public abstract class SqlExpressionVisitor
    {
        public SqlExpression Visit(SqlExpression expression)
        {
            return Visit((dynamic) expression);
        }

        protected virtual SqlExpression Visit(SqlQueryExpression expression)
        {
            Visit(expression.Select);
            Visit((SqlExpression) expression.Where);
            Visit(expression.OrderBy);
            return expression;
        }

        protected virtual SqlExpression Visit(SqlProjectionExpression expression)
        {
            return expression;
        }

        protected virtual SqlExpression Visit(SqlWhereExpression expression)
        {
            Visit((SqlExpression) expression.Predicate);
            return expression;
        }

        protected virtual SqlExpression Visit(SqlBinaryExpression expression)
        {
            Visit(expression.Left);
            Visit(expression.Right);
            return expression;
        }

        protected virtual SqlExpression Visit(SqlConstantExpression expression)
        {
            return expression;
        }

        protected virtual SqlExpression Visit(SqlColumnExpression expression)
        {
            return expression;
        }

        protected virtual SqlExpression Visit(SqlNotExpression expression)
        {
            return expression;
        }
    }
}