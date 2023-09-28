using System;
using System.Linq;
using HybridDb.Linq.Old.Ast;

namespace HybridDb.Linq.Old
{
    public abstract class SqlExpressionVisitor
    {
        public SqlExpression Visit(SqlExpression expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return Visit((dynamic)expression);
        }

        protected virtual SqlExpression Visit(SqlQueryExpression expression) =>
            new SqlQueryExpression(
                Visit(expression.Select),
                Visit(expression.Where),
                Visit(expression.OrderBy));

        protected virtual SqlExpression Visit(SqlSelectExpression expression) =>
            new SqlSelectExpression(expression.Projections.Select(x => (SqlProjectionExpression)Visit(x)));

        protected virtual SqlExpression Visit(SqlProjectionExpression expression) =>
            new SqlProjectionExpression((SqlColumnExpression)Visit(expression.From), expression.To);

        protected virtual SqlExpression Visit(SqlWhereExpression expression) =>
            new SqlWhereExpression((SqlBinaryExpression)Visit(expression.Predicate));

        protected virtual SqlExpression Visit(SqlOrderByExpression expression) =>
            new SqlOrderByExpression(expression.Columns.Select(x => (SqlOrderingExpression)Visit(x)));

        protected virtual SqlExpression Visit(SqlOrderingExpression expression) =>
            new SqlOrderingExpression(
                expression.Direction,
                (SqlColumnExpression)Visit(expression.Column));

        protected virtual SqlExpression Visit(SqlBinaryExpression expression) =>
            new SqlBinaryExpression(
                expression.NodeType,
                Visit(expression.Left),
                Visit(expression.Right));

        protected virtual SqlExpression Visit(SqlConstantExpression expression) => expression;

        protected virtual SqlExpression Visit(SqlColumnExpression expression) => expression;

        protected virtual SqlExpression Visit(SqlNotExpression expression) => new SqlNotExpression(Visit(expression.Operand));
    }
}