using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Old.Ast;

namespace HybridDb.Linq.Old.Parsers
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

        //protected override Expression VisitNew(NewExpression node)
        //{
        //    foreach (var argument in node.Arguments)
        //    {
        //        Visit(argument);
        //    }



        //    var type = node.Type;
        //    ast.Push(new SqlConstantExpression(type, node..Value));
        //    return expression;
        //}

        protected override Expression VisitConstant(ConstantExpression expression)
        {
            var type = expression.Value?.GetType() ?? typeof(object);
            ast.Push(new SqlConstantExpression(type, expression.Value));
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
            if (expression.Object == null)
            {
                var arguments = ast.Pop(expression.Arguments.Count)
                                   .Cast<SqlConstantExpression>()
                                   .Select(x => x.Value);

                ast.Push(new SqlConstantExpression(expression.Method.ReturnType, expression.Method.Invoke(null, arguments.ToArray())));
            }
            else
            {
                var receiver = ((SqlConstantExpression)ast.Pop()).Value;
                var arguments = ast.Pop(expression.Arguments.Count)
                                   .Cast<SqlConstantExpression>()
                                   .Select(x => x.Value);

                ast.Push(new SqlConstantExpression(expression.Method.ReturnType, expression.Method.Invoke(receiver, arguments.ToArray())));
            }
        }

        protected virtual void VisitColumnMethodCall(MethodCallExpression expression)
        {
            switch (expression.Method.Name)
            {
                case "Column":
                {
                    var column = ast.Pop() as SqlColumnExpression; // remove the current column expression
                    if (column == null || column.ColumnName != "")
                    {
                        throw new NotSupportedException($"{expression} method must be called on the lambda parameter.");
                    }

                    var constant = (SqlConstantExpression) ast.Pop();
                    var columnType = expression.Method.GetGenericArguments()[0];
                    var columnName = (string) constant.Value;

                    ast.Push(new SqlColumnExpression(columnType, columnName));
                    break;
                }
                case "Index":
                {
                    var column = ast.Pop() as SqlColumnExpression; // remove the current column expression
                    if (column == null || column.ColumnName != "")
                    {
                        throw new NotSupportedException($"{expression} method must be called on the lambda parameter.");
                    }

                    var type = expression.Method.GetGenericArguments()[0];

                    ast.Push(new SqlColumnPrefixExpression(type.Name));
                    break;
                }
                default:
                    ast.Pop();
                    var name = ColumnNameBuilder.GetColumnNameByConventionFor(expression);
                    ast.Push(new SqlColumnExpression(expression.Method.ReturnType, name));
                    break;
            }
        }

        protected override Expression VisitNewArray(NewArrayExpression expression)
        {
            var items = new object[0];

            if (expression.NodeType == ExpressionType.NewArrayInit)
            {
                items = new object[expression.Expressions.Count];

                for (var i = 0; i < expression.Expressions.Count; i++)
                {
                    Visit(expression.Expressions[i]);
                    items[i] = ((SqlConstantExpression) ast.Pop()).Value;
                }
            }

            ast.Push(new SqlConstantExpression(typeof(object[]), items));

            return expression;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression == null)
            {
                ast.Push(new SqlConstantExpression(expression.Member.GetMemberType(), expression.Member.GetValue(null)));
                return expression;
            }

            Visit(expression.Expression);

            switch (ast.Peek().NodeType)
            {
                case SqlNodeType.Constant:
                    var constant = (SqlConstantExpression) ast.Pop();
                    if (constant.Value == null)
                        throw new NullReferenceException();

                    ast.Push(new SqlConstantExpression(expression.Member.GetMemberType(), expression.Member.GetValue(constant.Value)));
                    break;
                case SqlNodeType.ColumnPrefix:
                    //TODO: clean up this mess. 
                    var prefix = (SqlColumnPrefixExpression)ast.Pop();
                    ast.Push(new SqlColumnExpression(expression.Member.GetMemberType(), expression.Member.Name));
                    break;
                case SqlNodeType.Column:
                    ast.Pop();
                    var name = ColumnNameBuilder.GetColumnNameByConventionFor(expression);
                    ast.Push(new SqlColumnExpression(expression.Member.GetMemberType(), name));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return expression;
        }
    }
}