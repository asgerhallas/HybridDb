using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HybridDb.Linq.Old.Ast;

namespace HybridDb.Linq.Old
{
    public class SqlCodeGenerator : SqlExpressionVisitor
    {
        readonly StringBuilder sql;
        readonly Dictionary<object, string> parameters;

        public SqlCodeGenerator(StringBuilder sql, Dictionary<object, string> parameters)
        {
            this.sql = sql;
            this.parameters = parameters;
        }

        protected override SqlExpression Visit(SqlBinaryExpression expression)
        {
            sql.Append("(");

            switch (expression.NodeType)
            {
                case SqlNodeType.BitwiseAnd:
                case SqlNodeType.And:
                case SqlNodeType.BitwiseOr:
                case SqlNodeType.Or:
                case SqlNodeType.LessThan:
                case SqlNodeType.LessThanOrEqual:
                case SqlNodeType.GreaterThan:
                case SqlNodeType.GreaterThanOrEqual:
                case SqlNodeType.Equal:
                case SqlNodeType.NotEqual:
                case SqlNodeType.In:
                case SqlNodeType.Is:
                case SqlNodeType.IsNot:
                    Visit(expression.Left);
                    sql.Append(GetOperator(expression));
                    Visit(expression.Right);
                    break;
                case SqlNodeType.LikeStartsWith:
                    Visit(expression.Left);
                    sql.Append(" LIKE ");
                    Visit(expression.Right);
                    sql.Append(" + '%'");
                    break;
                case SqlNodeType.LikeContains:
                    Visit(expression.Left);
                    sql.Append(" LIKE ");
                    sql.Append("'%' + ");
                    Visit(expression.Right);
                    sql.Append(" + '%'");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            sql.Append(")");

            return expression;
        }

        string GetOperator(SqlBinaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case SqlNodeType.BitwiseAnd:
                    return "&";
                case SqlNodeType.And:
                    return " AND ";
                case SqlNodeType.BitwiseOr:
                    return "|";
                case SqlNodeType.Or:
                    return " OR ";
                case SqlNodeType.LessThan:
                    return " < ";
                case SqlNodeType.LessThanOrEqual:
                    return " <= ";
                case SqlNodeType.GreaterThan:
                    return " > ";
                case SqlNodeType.GreaterThanOrEqual:
                    return " >= ";
                case SqlNodeType.Equal:
                    return " = ";
                case SqlNodeType.NotEqual:
                    return " <> ";
                case SqlNodeType.In:
                    return " IN ";
                case SqlNodeType.Is:
                    return " IS ";
                case SqlNodeType.IsNot:
                    return " IS NOT ";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override SqlExpression Visit(SqlColumnExpression expression)
        {
            sql.Append($"[{expression.ColumnName}]");
            return expression;
        }

        protected override SqlExpression Visit(SqlNotExpression expression)
        {
            sql.Append(" NOT ");
            Visit(expression.Operand);
            return expression;
        }

        protected override SqlExpression Visit(SqlOrderByExpression expression)
        {
            sql.Append(string.Join(", ", expression.Columns.Select(FormatOrdering)));
            return expression;
        }

        string FormatOrdering(SqlOrderingExpression expression)
        {
            return $"[{expression.Column.ColumnName}]{(expression.Direction == SqlOrderingExpression.Directions.Descending ? " DESC" : "")}";
        }

        protected override SqlExpression Visit(SqlSelectExpression expression)
        {
            sql.Append(string.Join(", ", expression.Projections.Select(FormatProjection)));
            return expression;
        }

        string FormatProjection(SqlProjectionExpression expression)
        {
            return $"[{expression.From.ColumnName}] AS {expression.To}";
        }

        protected override SqlExpression Visit(SqlConstantExpression expression)
        {
            sql.Append(FormatConstant(expression.Value));
            return expression;
        }

        string FormatConstant(object value)
        {
            if (value == null)
                return "NULL";

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                var listOfValues = enumerable.Cast<object>().ToList();
                return $"({(listOfValues.Count == 0 ? "NULL" : string.Join(", ", listOfValues.Select(FormatConstant)))})";
            }

            string key;
            if (parameters.TryGetValue(value, out key))
                return key;

            key = "@Value" + parameters.Count;
            parameters.Add(value, key);
            return key;
        }
    }
}