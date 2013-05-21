using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb
{
    public class NullCheckInjector : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object != null)
            {
                return Expression.Condition(
                    Expression.Equal(Visit(node.Object), Expression.Constant(null)),
                    Expression.Convert(Expression.Constant(null), node.Method.ReturnType),
                    node);
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var property = node.Member as PropertyInfo;
            if (property != null)
            {
                return Expression.Condition(
                    Expression.Equal(Visit(node.Expression), Expression.Constant(null)),
                    Expression.Convert(Expression.Constant(null), property.PropertyType),
                    node);
            }

            var field = node.Member as FieldInfo;
            if (field != null)
            {
                return Expression.Condition(
                    Expression.Equal(Visit(node.Expression), Expression.Constant(null)),
                    Expression.Convert(Expression.Constant(null), field.FieldType),
                    node);
            }

            return base.VisitMember(node);
        }
    }
}