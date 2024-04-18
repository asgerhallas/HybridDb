using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HybridDb
{
    public class NullCheckInjector : ExpressionVisitor
    {
        public bool CanBeTrustedToNeverReturnNull { get; private set; }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            CanBeTrustedToNeverReturnNull = true;

            return Expression.Lambda(
                Expression.Convert(Visit(node.Body), typeof(object)),
                node.Parameters);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.TypeAs:
                    CanBeTrustedToNeverReturnNull = false;
                    break;
                case ExpressionType.Convert:
                    return Visit(node.Operand);
            }

            return base.VisitUnary(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value == null)
            {
                CanBeTrustedToNeverReturnNull = false;
            }

            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var receiver = node.Object;

            // Check for extension method
            if (receiver == null && node.Method.IsDefined(typeof(ExtensionAttribute)))
            {
                receiver = node.Arguments[0];
            }

            var nullCheckedReceiver = receiver != null ? Visit(receiver) : node;

            if (node.Type.CanBeNull())
            {
                CanBeTrustedToNeverReturnNull = false;
            }

            if (!CanBeNull(nullCheckedReceiver))
            {
                return node;
            }

            return Expression.Condition(
                Expression.ReferenceNotEqual(nullCheckedReceiver, Expression.Constant(null)),
                Expression.Convert(node, typeof(object)),
                Expression.Convert(Expression.Constant(null), typeof(object)));
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var nullCheckedReceiver = Visit(node.Expression);

            if (node.Type.CanBeNull())
            {
                CanBeTrustedToNeverReturnNull = false;
            }

            if (!CanBeNull(nullCheckedReceiver) /* && !node.Type.CanBeNull()*/)
            {
                return node;
            }

            return Expression.Condition(
                Expression.ReferenceNotEqual(nullCheckedReceiver, Expression.Constant(null)),
                Expression.Convert(node, typeof(object)),
                Expression.Convert(Expression.Constant(null), typeof(object)));
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = Visit(node.Left)!;
            var right = Visit(node.Right)!;

            return Expression.MakeBinary(node.NodeType, left, right);
        }

        bool CanBeNull(Expression expression) =>
            // We do not allow parameters to be null
            expression.NodeType != ExpressionType.Parameter &&
            expression.Type.CanBeNull();
    }
}