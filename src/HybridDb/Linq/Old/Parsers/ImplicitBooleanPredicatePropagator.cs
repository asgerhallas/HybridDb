using HybridDb.Linq.Old.Ast;

namespace HybridDb.Linq.Old.Parsers
{
    public class ImplicitBooleanPredicatePropagator : SqlExpressionVisitor
    {
        protected override SqlExpression Visit(SqlBinaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case SqlNodeType.And:
                case SqlNodeType.BitwiseAnd:
                case SqlNodeType.Or:
                case SqlNodeType.BitwiseOr:
                    return base.Visit(expression);
            }

            return expression;
        }

        protected override SqlExpression Visit(SqlConstantExpression expression)
        {
            if (expression.Value is bool)
            {
                var nodeType = ((bool) expression.Value) ? SqlNodeType.Equal : SqlNodeType.NotEqual;

                return new SqlBinaryExpression(nodeType, 
                    new SqlConstantExpression(typeof(int), 1),
                    new SqlConstantExpression(typeof(int), 1));
            }

            return base.Visit(expression);
        }

        protected override SqlExpression Visit(SqlColumnExpression expression)
        {
            if (expression.Type == typeof(bool))
                return new SqlBinaryExpression(SqlNodeType.Equal, expression, new SqlConstantExpression(typeof(bool), true));
            
            return expression;
        }
    }
}