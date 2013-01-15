using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    internal class StripQuotesVisitor : ExpressionVisitor
    {
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Quote)
                return Visit(node.Operand);

            return base.VisitUnary(node);
        }
    }

    internal class ClauseExtractionVisitor : ExpressionVisitor
    {
        SqlExpression orderBy;
        SqlExpression select;
        SqlWhereExpression where;

        public SqlQueryExpression Translate(Expression expression)
        {
            Visit(expression);
            return new SqlQueryExpression(new SqlExpression(), where, new SqlExpression());
        }

        static Expression StripQuotes(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
                e = ((UnaryExpression)e).Operand;

            return e;
        }


        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            switch (expression.Method.Name)
            {
                case "Where":
                    where = new WhereVisitor().Translate(((LambdaExpression)StripQuotes(expression.Arguments[1])).Body);
                    break;
                case "Select":
                    select = new SelectVisitor().Translate(expression.Arguments[1]);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The method {0} is not supported", expression.Method.Name));
            }

            Visit(expression.Arguments[0]);
            return expression;
        }
    }

    [DebuggerDisplay("{NodeType}, Value = {Value}")]
    internal class Product
    {
        public SqlNodeType NodeType { get; set; }
        public object Value { get; set; }
    }

    internal class WhereVisitor : ExpressionVisitor
    {
        readonly Stack<Product> nodes = new Stack<Product>();
        readonly Stack<string> members = new Stack<string>();

        public SqlWhereExpression Translate(Expression expression)
        {
            Visit(expression);
            return new SqlWhereExpression((SqlBinaryExpression) ConvertToSqlExpressions());
        }

        public SqlExpression ConvertToSqlExpressions()
        {
            var arguments = new Stack<SqlExpression>();
            while (nodes.Count > 0)
            {
                var current = nodes.Pop();

                switch (current.NodeType)
                {
                    case SqlNodeType.And:
                    case SqlNodeType.Or:
                    case SqlNodeType.Equal:
                        arguments.Push(new SqlBinaryExpression(current.NodeType, arguments.Pop(), arguments.Pop()));
                        break;
                    case SqlNodeType.Column:
                        var columnName = "";
                        while (nodes.Count > 0 && nodes.Peek().NodeType == SqlNodeType.Argument)
                        {
                            columnName += ((MemberExpression) nodes.Pop().Value).Member.Name;
                        }

                        arguments.Push(new SqlColumnExpression(columnName));
                        break;
                    case SqlNodeType.Constant:
                        object constant = current.Value;
                        while (nodes.Count > 0 && nodes.Peek().NodeType == SqlNodeType.Argument)
                        {
                            var expression = (MemberExpression) nodes.Pop().Value;
                            constant = expression.Member.GetValue(constant);
                        }
                        arguments.Push(new SqlConstantExpression(constant));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (arguments.Count > 1)
                throw new Exception();

            return arguments.Pop();
        }

        protected override Expression VisitBinary(BinaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    nodes.Push(new Product {NodeType = SqlNodeType.And, Value = null});
                    break;
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    nodes.Push(new Product { NodeType = SqlNodeType.Or, Value = null });
                    break;
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                    break;
                case ExpressionType.Equal:
                    nodes.Push(new Product { NodeType = SqlNodeType.Equal, Value = null });
                    break;
                case ExpressionType.NotEqual:
                    break;
                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", expression.NodeType));
            }

            Visit(expression.Left);
            Visit(expression.Right);
            return expression;
        }

        protected override Expression VisitConstant(ConstantExpression expression)
        {
            nodes.Push(new Product { NodeType = SqlNodeType.Constant, Value = expression });
            return expression;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            nodes.Push(new Product { NodeType = SqlNodeType.Argument, Value = expression});
            return base.VisitMember(expression);
        }

        protected override Expression VisitParameter(ParameterExpression expression)
        {
            nodes.Push(new Product { NodeType = SqlNodeType.Column, Value = "" });
            //nodes.Push(SqlNodeType.Column);
            return expression;
        }
    }

    internal class SelectVisitor : ExpressionVisitor
    {
        public SqlWhereExpression Translate(Expression expression)
        {
            return null;
        }
    }
}