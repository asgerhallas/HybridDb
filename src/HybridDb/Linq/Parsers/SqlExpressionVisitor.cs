using System;
using HybridDb.Linq.Ast;
using System.Linq;

namespace HybridDb.Linq.Parsers
{
    public abstract class SqlExpressionVisitor
    {
        public SqlExpression Visit(SqlExpression expression)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");

            return Visit((dynamic) expression);
        }

        protected virtual SqlExpression Visit(SqlQueryExpression expression)
        {
            return new SqlQueryExpression(Visit(expression.Select), Visit(expression.Where), Visit(expression.OrderBy));
        }

        protected virtual SqlExpression Visit(SqlSelectExpression expression)
        {
            return new SqlSelectExpression(expression.Projections.Select(x => (SqlProjectionExpression)Visit(x)));
        }

        protected virtual SqlExpression Visit(SqlProjectionExpression expression)
        {
            return new SqlProjectionExpression((SqlColumnExpression) Visit(expression.From), expression.To);
        }

        protected virtual SqlExpression Visit(SqlWhereExpression expression)
        {
            return new SqlWhereExpression((SqlBinaryExpression) Visit(expression.Predicate));
        }

        protected virtual SqlExpression Visit(SqlOrderByExpression expression)
        {
            return new SqlOrderByExpression(expression.Columns.Select(x => (SqlOrderingExpression) Visit(x)));
        }

        protected virtual SqlExpression Visit(SqlOrderingExpression expression)
        {
            return new SqlOrderingExpression(expression.Direction, (SqlColumnExpression) Visit(expression.Column));
        }

        protected virtual SqlExpression Visit(SqlBinaryExpression expression)
        {
            return new SqlBinaryExpression(expression.NodeType, Visit(expression.Left), Visit(expression.Right));
        }

        protected virtual SqlExpression Visit(SqlConstantExpression expression)
        {
            return expression;
        }

        protected virtual SqlExpression Visit(SqlColumnExpression expression)
        {
            return expression;
        }

        protected virtual SqlExpression Visit(SqlNotExpression expression)
        {
            return new SqlNotExpression(Visit(expression.Operand));
        }
    }
}