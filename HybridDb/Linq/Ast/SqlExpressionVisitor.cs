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
            Visit(expression.Where);
            Visit(expression.OrderBy);
            return expression;
        }

        protected virtual SqlExpression Visit(SqlProjectionExpression expression)
        {
            Visit(expression.From);
            return expression;
        }

        protected virtual SqlExpression Visit(SqlWhereExpression expression)
        {
            Visit(expression.Predicate);
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

    public class NullCheckVisitor : SqlExpressionVisitor
    {
        protected override SqlExpression Visit(SqlBinaryExpression expression)
        {
            if (expression.Right.NodeType == SqlNodeType.Constant && ((SqlConstantExpression)expression.Right).Value == null)

            switch (expression.NodeType)
            {
                    case SqlNodeType.Equal:
                    case SqlNodeType.NotEqual:
                        if (expression.Right.NodeType == SqlNodeType.Constant && ((SqlConstantExpression)expression.Right).Value == null)
                            return new SqlBinaryExpression(expression.NodeType, expression.Left, e);
                            
            }
        }
    }
}