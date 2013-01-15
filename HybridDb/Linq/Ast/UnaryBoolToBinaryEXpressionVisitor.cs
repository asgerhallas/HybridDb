using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    internal class UnaryBoolToBinaryEXpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return base.VisitBinary(node);
            }

            return node;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            var member = expression.GetMemberExpressionInfo();
            if (member != null)
            {
                if (member.ResultingType == typeof (bool))
                {
                    return Expression.MakeBinary(ExpressionType.Equal, expression, Expression.Constant(true));
                }
            }
            return expression;
        }
    }
}