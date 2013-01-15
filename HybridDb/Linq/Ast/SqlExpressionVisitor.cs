using System;
using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    internal abstract class SqlExpressionVisitor : ExpressionVisitor
    {
        public SqlExpression Visit(SqlExpression expression)
        {
            switch (expression.NodeType)
            {
                case SqlNodeType.Query:
                    return VisitQuery((SqlQueryExpression) expression);
                case SqlNodeType.Select:
                    return VisitSelect(expression);
                case SqlNodeType.Where:
                    return VisitWhere((SqlWhereExpression) expression);
                case SqlNodeType.And:
                case SqlNodeType.Or:
                case SqlNodeType.Equal:
                    return VisitBinary((SqlBinaryExpression) expression);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected virtual SqlExpression VisitQuery(SqlQueryExpression expression)
        {
            Visit(expression.Select);
            Visit(expression.Where);
            Visit(expression.OrderBy);
            return expression;
        }

        protected virtual SqlExpression VisitSelect(SqlExpression expression)
        {
            throw new NotImplementedException();
            return expression;
        }

        protected virtual SqlExpression VisitWhere(SqlWhereExpression expression)
        {
            Visit(expression.Predicate);
            return expression;
        }

        protected virtual SqlExpression VisitBinary(SqlBinaryExpression expression)
        {
            Visit(expression.Left);
            Visit(expression.Right);
            return expression;
        }
    }
}