using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

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

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.TypeAs)
            {
                var assignCurrentValue = Expression.Assign(currentValue, Expression.Convert(node, typeof(object)));

                CanBeTrustedToNeverReturnNull = false;

                return Expression.Block(
                    Visit(node.Operand),
                    assignCurrentValue,
                    Expression.IfThen(
                        Expression.Equal(currentValue, Expression.Constant(null)),
                        Expression.Return(returnTarget, Expression.Constant(null))));
            }

            if (node.NodeType == ExpressionType.Convert)
                return Visit(node.Operand);

            return base.VisitUnary(node);
        }

        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        {
            return Expression.TypeIs(Visit(node.Expression), node.TypeOperand);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value == null)
            {
                CanBeTrustedToNeverReturnNull = false;
                return Expression.Return(returnTarget, Expression.Convert(node, typeof(object)));
            }

            return Expression.Assign(currentValue, Expression.Convert(node, typeof(object)));
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var receiver = node.Object;

            // Check for extension method
            if (receiver == null && node.Method.IsDefined(typeof(ExtensionAttribute)))
                receiver = node.Arguments[0];

            var nullCheckedBlock = receiver != null ? Visit(receiver): node;

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