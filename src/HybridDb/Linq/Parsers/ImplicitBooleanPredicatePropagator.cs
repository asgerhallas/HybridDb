using System.Runtime.InteropServices;
using HybridDb.Linq.Ast;

namespace HybridDb.Linq.Parsers
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

        protected override SqlExpression Visit(Constant expression)
        {
            if (expression.Value is bool)
            {
                var nodeType = ((bool) expression.Value) ? SqlNodeType.Equal : SqlNodeType.NotEqual;

                return new SqlBinaryExpression(nodeType, 
                    new Constant(typeof(int), 1),
                    new Constant(typeof(int), 1));
            }

            return base.Visit(expression);
        }

        protected override SqlExpression Visit(SqlColumnExpression expression)
        {
            if (expression.Type == typeof(bool))
                return new SqlBinaryExpression(SqlNodeType.Equal, expression, new Constant(typeof(bool), true));
            
            return expression;
        }
    }
}