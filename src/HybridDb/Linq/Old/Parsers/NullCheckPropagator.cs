using HybridDb.Linq.Old.Ast;

namespace HybridDb.Linq.Old.Parsers
{
    public class NullCheckPropagator : SqlExpressionVisitor
    {
        protected override SqlExpression Visit(SqlBinaryExpression expression)
        {
            if (expression.Right.NodeType == SqlNodeType.Constant &&
                ((SqlConstantExpression) expression.Right).Value == null)
            {
                switch (expression.NodeType)
                {
                    case SqlNodeType.Equal:
                        return new SqlBinaryExpression(SqlNodeType.Is, expression.Left, new SqlConstantExpression(typeof(object), null));
                    case SqlNodeType.NotEqual:
                        return new SqlBinaryExpression(SqlNodeType.IsNot, expression.Left, new SqlConstantExpression(typeof(object), null));
                }
            }

            return base.Visit(expression);
        }
    }
}