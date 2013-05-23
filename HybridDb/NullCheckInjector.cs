using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb
{
    public class NullCheckInjector : ExpressionVisitor
    {
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            // Change the return type of the lambda to the new body's return type
            return Expression.Lambda(Visit(node.Body), node.Parameters);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object != null)
            {
                return Expression.Condition(
                    Expression.Equal(Visit(node.Object), Expression.Constant(null)),
                    Expression.Convert(Expression.Constant(null), typeof(object)),
                    Expression.Convert(node, typeof(object)));
            }

            return Expression.Convert(node, typeof(object));
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var property = node.Member as PropertyInfo;
            if (property != null)
            {
                var expression = Visit(node.Expression);
                var condition = Expression.Condition(
                    Expression.Equal(expression, Expression.Constant(null)),
                    Expression.Convert(Expression.Constant(null), typeof(object)),
                    Expression.Convert(node, typeof(object)));
                return condition;
            }

            var field = node.Member as FieldInfo;
            if (field != null)
            {
                return Expression.Condition(
                    Expression.Equal(Visit(node.Expression), Expression.Constant(null)),
                    Expression.Convert(Expression.Constant(null), typeof(object)),
                    Expression.Convert(node, typeof(object)));
            }

            return node;
        }
    }
}