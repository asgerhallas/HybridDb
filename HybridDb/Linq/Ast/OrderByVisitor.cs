using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    internal class OrderByVisitor : ExpressionVisitor
    {
        readonly Stack<Operation> operations;

        public OrderByVisitor(Stack<Operation> operations)
        {
            this.operations = operations;
        }

        protected override Expression VisitLambda<T>(Expression<T> expression)
        {
            return Visit(expression.Body);
        }

        protected override Expression VisitUnary(UnaryExpression expression)
        {
            if (expression.NodeType == ExpressionType.Quote)
                Visit(expression.Operand);

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