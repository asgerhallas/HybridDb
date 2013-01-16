using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    internal class WhereVisitor : ExpressionVisitor
    {
        readonly Stack<Operation> operations;

        public WhereVisitor(Stack<Operation> operations)
        {
            this.operations = operations;
        }

        public static Stack<Operation> Translate(Expression expression)
        {
            var operations = new Stack<Operation>();
            expression = new UnaryBoolToBinaryExpressionVisitor().Visit(expression);
            new WhereVisitor(operations).Visit(expression);
            return operations;
        }

        public override Expression Visit(Expression node)
        {
            
            return base.Visit(node);
        }

        protected override Expression VisitBinary(BinaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.And:
                    operations.Push(new Operation(SqlNodeType.BitwiseAnd));
                    break;
                case ExpressionType.AndAlso:
                    operations.Push(new Operation(SqlNodeType.And));
                    break;
                case ExpressionType.Or:
                    operations.Push(new Operation(SqlNodeType.BitwiseOr));
                    break;
                case ExpressionType.OrElse:
                    operations.Push(new Operation(SqlNodeType.Or));
                    break;
                case ExpressionType.LessThan:
                    operations.Push(new Operation(SqlNodeType.LessThan));
                    break;
                case ExpressionType.LessThanOrEqual:
                    operations.Push(new Operation(SqlNodeType.LessThanOrEqual));
                    break;
                case ExpressionType.GreaterThan:
                    operations.Push(new Operation(SqlNodeType.GreaterThan));
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    operations.Push(new Operation(SqlNodeType.GreaterThanOrEqual));
                break;
                case ExpressionType.Equal:
                    operations.Push(new Operation(SqlNodeType.Equal));
                    break;
                case ExpressionType.NotEqual:
                    operations.Push(new Operation(SqlNodeType.NotEqual));
                    break;
                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", expression.NodeType));
            }

            Visit(expression.Left);
            Visit(expression.Right);

            return expression;
        }

        protected override Expression VisitLambda<T>(Expression<T> expression)
        {
            return Visit(expression.Body);
        }

        protected override Expression VisitUnary(UnaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Not:
                    operations.Push(new Operation(SqlNodeType.Not));
                    Visit(expression.Operand);
                    break;
                case ExpressionType.Quote:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    Visit(expression.Operand);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", expression.NodeType));
            }

            return expression;
        }

        protected override Expression VisitConstant(ConstantExpression expression)
        {
            operations.Push(new Operation(SqlNodeType.Constant, expression.Value));
            return expression;
        }

        protected override Expression VisitParameter(ParameterExpression expression)
        {
            operations.Push(new Operation(SqlNodeType.Column, ""));
            return expression;
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            Visit(expression.Arguments);
            Visit(expression.Object);

            var operation = operations.Pop();
            switch (operation.NodeType)
            {
                case SqlNodeType.Constant:
                    var target = operation.Value;
                    var arguments = operations.Pop(expression.Arguments.Count)
                                              .Select(x => x.Value);

                    operations.Push(new Operation(
                                        SqlNodeType.Constant,
                                        expression.Method.Invoke(target, arguments.ToArray())));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return expression;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression == null)
            {
                operations.Push(new Operation(SqlNodeType.Constant, expression.Member.GetValue(null)));
                return expression;
            }

            Visit(expression.Expression);

            switch (operations.Peek().NodeType)
            {
                case SqlNodeType.Constant:
                    operations.Push(new Operation(
                                        SqlNodeType.Constant,
                                        expression.Member.GetValue(operations.Pop().Value)));
                    break;
                case SqlNodeType.Column:
                    operations.Push(new Operation(
                                        SqlNodeType.Column,
                                        operations.Pop().Value + expression.Member.Name));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return expression;
        }
    }
}