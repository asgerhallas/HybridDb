using System;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb
{
    public class NullCheckInjector : ExpressionVisitor
    {
        readonly ParameterExpression currentValue;
        readonly LabelTarget returnTarget;

        public NullCheckInjector()
        {
            currentValue = Expression.Variable(typeof(object));
            returnTarget = Expression.Label(typeof(object));
        }

        public bool CanBeTrustedToNeverReturnNull { get; private set; }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            CanBeTrustedToNeverReturnNull = true;

            // Change the return type of the lambda to the new body's return type
            var nullCheckedBody = Expression.Block(
                new[] { currentValue },
                Visit(node.Body),
                Expression.Label(returnTarget, currentValue));

            return Expression.Lambda(nullCheckedBody, node.Parameters);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object != null)
            {
                var nullCheckedBlock = Visit(node.Object);

                var assignCurrentValue = Expression.Assign(currentValue, Expression.Convert(node, typeof(object)));

                if (!node.Method.ReturnType.CanBeNull())
                {
                    return Expression.Block(nullCheckedBlock, assignCurrentValue);
                }

                CanBeTrustedToNeverReturnNull = false;

                return Expression.Block(
                    nullCheckedBlock,
                    assignCurrentValue,
                    Expression.IfThen(
                        Expression.Equal(currentValue, Expression.Constant(null)),
                        Expression.Return(returnTarget, Expression.Constant(null))));
            }

            return Expression.Convert(node, typeof(object));
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var nullCheckedBlock = Visit(node.Expression);

            Type type;
            if (node.Member is PropertyInfo) type = ((PropertyInfo)node.Member).PropertyType;
            else if (node.Member is FieldInfo) type = ((FieldInfo) node.Member).FieldType;
            else throw new ArgumentOutOfRangeException("Member access of type " + node.Member.GetType() + " is not supported.");

            var assignCurrentValue = Expression.Assign(currentValue, Expression.Convert(node, typeof (object)));

            if (!type.CanBeNull())
            {
                return Expression.Block(nullCheckedBlock, assignCurrentValue);
            }

            CanBeTrustedToNeverReturnNull = false;

            return Expression.Block(
                nullCheckedBlock,
                assignCurrentValue,
                Expression.IfThen(
                    Expression.Equal(currentValue, Expression.Constant(null)),
                    Expression.Return(returnTarget, Expression.Constant(null))));
        }
    }
}