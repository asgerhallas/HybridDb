using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;
using ShinySwitch;

namespace HybridDb.Linq.Parsers
{
    public class LambdaParser : ExpressionVisitor
    {
        protected readonly Stack<AstNode> ast;

        public LambdaParser(Stack<AstNode> ast)
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
            var type = expression.Value?.GetType() ?? typeof(object);
            ast.Push(new Constant(type, expression.Value));
            return expression;
        }

        protected override Expression VisitParameter(ParameterExpression expression)
        {
            ast.Push(new ColumnIdentifier(typeof(object), ""));
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

            Switch.On(ast.Peek())
                .Match<Constant>(_ => VisitConstantMethodCall(expression))
                .Match<ColumnIdentifier>(_ => VisitColumnMethodCall(expression))
                .OrThrow(new ArgumentOutOfRangeException());

            return expression;
        }

        protected virtual void VisitConstantMethodCall(MethodCallExpression expression)
        {
            if (expression.Object == null)
            {
                var arguments = ast.Pop(expression.Arguments.Count)
                                   .Cast<Constant>()
                                   .Select(x => x.Value);

                ast.Push(new Constant(expression.Method.ReturnType, expression.Method.Invoke(null, arguments.ToArray())));
            }
            else
            {
                var receiver = ((Constant)ast.Pop()).Value;
                var arguments = ast.Pop(expression.Arguments.Count)
                                   .Cast<Constant>()
                                   .Select(x => x.Value);

                ast.Push(new Constant(expression.Method.ReturnType, expression.Method.Invoke(receiver, arguments.ToArray())));
            }
        }

        protected virtual void VisitColumnMethodCall(MethodCallExpression expression)
        {
            switch (expression.Method.Name)
            {
                case "Column":
                {
                    var column = ast.Pop() as ColumnIdentifier; // remove the current column expression
                    if (column == null || column.ColumnName != "")
                    {
                        throw new NotSupportedException($"{expression} method must be called on the lambda parameter.");
                    }

                    var constant = (Constant) ast.Pop();
                    var columnType = expression.Method.GetGenericArguments()[0];
                    var columnName = (string) constant.Value;

                    ast.Push(new ColumnIdentifier(columnType, columnName));
                    break;
                }
                case "Index":
                {
                    var column = ast.Pop() as ColumnIdentifier; // remove the current column expression
                    if (column == null || column.ColumnName != "")
                    {
                        throw new NotSupportedException($"{expression} method must be called on the lambda parameter.");
                    }

                    var type = expression.Method.GetGenericArguments()[0];

                    //TODO:
                    //ast.Push(new SqlColumnPrefixExpression(type.Name));
                    break;
                }
                default:
                    ast.Pop();
                    var name = ColumnNameBuilder.GetColumnNameByConventionFor(expression);
                    ast.Push(new ColumnIdentifier(expression.Method.ReturnType, name));
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
                    items[i] = ((Constant) ast.Pop()).Value;
                }
            }

            ast.Push(new Constant(typeof(object[]), items));

            return expression;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression == null)
            {
                ast.Push(new Constant(expression.Member.GetMemberType(), expression.Member.GetValue(null)));
                return expression;
            }

            Visit(expression.Expression);

            Switch.On(ast.Peek())
                .Match<Constant>(x =>
                {
                    var constant = (Constant) ast.Pop();
                    if (constant.Value == null)
                        throw new NullReferenceException();

                    ast.Push(new Constant(expression.Member.GetMemberType(), expression.Member.GetValue(constant.Value)));
                })
                .Match<ColumnIdentifier>(x =>
                {
                    ast.Pop();
                    var name = ColumnNameBuilder.GetColumnNameByConventionFor(expression);
                    ast.Push(new ColumnIdentifier(expression.Member.GetMemberType(), name));
                })
                .OrThrow(new ArgumentOutOfRangeException());

            //TODO:
            //    case SqlNodeType.ColumnPrefix:
            //        //TODO: clean up this mess. 
            //        var prefix = (SqlColumnPrefixExpression)ast.Pop();
            //ast.Push(new SqlColumnExpression(expression.Member.GetMemberType(), expression.Member.Name));
            //break;
            

            return expression;
        }
    }
}