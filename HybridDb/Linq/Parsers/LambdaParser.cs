using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Ast;

namespace HybridDb.Linq.Parsers
{
    public class LambdaParser : ExpressionVisitor
    {
        protected readonly Stack<SqlExpression> ast;

        public LambdaParser(Stack<SqlExpression> ast)
        {
            this.ast = ast;
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
            ast.Push(new SqlConstantExpression(expression.Value));
            return expression;
        }

        protected override Expression VisitParameter(ParameterExpression expression)
        {
            ast.Push(new SqlColumnExpression(expression.Type, ""));
            return expression;
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            if (expression.Object == null)
            {
                Visit(expression.Arguments.Skip(1).ToReadOnlyCollection());
                Visit(expression.Arguments.Take(1).ToReadOnlyCollection());
            }
            else
            {
                Visit(expression.Arguments);
                Visit(expression.Object);
            }

            switch (ast.Peek().NodeType)
            {
                case SqlNodeType.Constant:
                    VisitConstantMethodCall(expression);
                    break;
                case SqlNodeType.Column:
                    VisitColumnMethodCall(expression);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return expression;
        }

        protected virtual void VisitConstantMethodCall(MethodCallExpression expression)
        {
            var target = ((SqlConstantExpression) ast.Pop()).Value;
            var arguments = ast.Pop(expression.Arguments.Count)
                               .Cast<SqlConstantExpression>()
                               .Select(x => x.Value);

            ast.Push(new SqlConstantExpression(expression.Method.Invoke(target, arguments.ToArray())));
        }

        protected virtual void VisitColumnMethodCall(MethodCallExpression expression)
        {
            switch (expression.Method.Name)
            {
                case "Column":
                    var constant = (ConstantExpression)expression.Arguments[1];
                    ast.Push(new SqlColumnExpression(expression.Method.GetGenericArguments()[0], (string) constant.Value));
                    break;
                default:
                    ast.Pop();
                    var name = new Configuration().GetColumnNameByConventionFor(expression);
                    ast.Push(new SqlColumnExpression(expression.Method.ReturnType, name));
                    break;
            }
        }

        protected override Expression VisitNewArray(NewArrayExpression expression)
        {
            var items = new object[expression.Expressions.Count];
            for (var i = 0; i < expression.Expressions.Count; i++)
            {
                Visit(expression.Expressions[i]);
                items[i] = ((SqlConstantExpression) ast.Pop()).Value;
            }

            ast.Push(new SqlConstantExpression(items));

            return expression;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression == null)
            {
                ast.Push(new SqlConstantExpression(expression.Member.GetValue(null)));
                return expression;
            }

            Visit(expression.Expression);

            switch (ast.Peek().NodeType)
            {
                case SqlNodeType.Constant:
                    var constant = (SqlConstantExpression) ast.Pop();
                    if (constant.Value == null)
                        throw new NullReferenceException();

                    ast.Push(new SqlConstantExpression(expression.Member.GetValue(constant.Value)));
                    break;
                case SqlNodeType.Column:
                    ast.Pop();
                    var name = new Configuration().GetColumnNameByConventionFor(expression);
                    ast.Push(new SqlColumnExpression(expression.Member.GetMemberType(), name));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return expression;
        }
    }
}