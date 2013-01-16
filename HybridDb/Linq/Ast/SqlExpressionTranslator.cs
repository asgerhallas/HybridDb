using System;
using System.Text;

namespace HybridDb.Linq.Ast
{
    public class SqlExpressionTranslator : SqlExpressionVisitor
    {
        readonly StringBuilder sql;

        public SqlExpressionTranslator(StringBuilder sql)
        {
            this.sql = sql;
        }

        protected override SqlExpression Visit(SqlProjectionExpression expression)
        {
            sql.Append(expression.From + " AS " + expression.To);
            return expression;
        }

        protected override SqlExpression Visit(SqlBinaryExpression expression)
        {
            sql.Append("(");

            Visit(expression.Left);

            switch (expression.NodeType)
            {
                case SqlNodeType.BitwiseAnd:
                    sql.Append("&");
                    break;
                case SqlNodeType.And:
                    sql.Append(" AND ");
                    break;
                case SqlNodeType.BitwiseOr:
                    sql.Append("|");
                    break;
                case SqlNodeType.Or:
                    sql.Append(" OR ");
                    break;
                case SqlNodeType.LessThan:
                    sql.Append(" < ");
                    break;
                case SqlNodeType.LessThanOrEqual:
                    sql.Append(" <= ");
                    break;
                case SqlNodeType.GreaterThan:
                    sql.Append(" > ");
                    break;
                case SqlNodeType.GreaterThanOrEqual:
                    sql.Append(" >= ");
                    break;
                case SqlNodeType.Equal:
                    sql.Append(" = ");
                    break;
                case SqlNodeType.NotEqual:
                    sql.Append(" <> ");
                    break;
                case SqlNodeType.Is:
                    sql.Append(" IS ");
                    break;
                case SqlNodeType.IsNot:
                    sql.Append(" IS NOT ");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Visit(expression.Right);

            sql.Append(")");

            return expression;
        }

        protected override SqlExpression Visit(SqlColumnExpression expression)
        {
            sql.Append(expression.ColumnName);
            return expression;
        }

        protected override SqlExpression Visit(SqlNotExpression expression)
        {
            sql.Append(" NOT ");
            Visit(expression.Operand);
            return expression;
        }

        protected override SqlExpression Visit(SqlConstantExpression expression)
        {
            if (expression.Value == null)
            {
                sql.Append("NULL");
            }
            else if (expression.Value is Boolean)
            {
                sql.Append(((bool) expression.Value) ? 1 : 0);
            }
            else if (expression.Value is String || expression.Value is Guid)
            {
                sql.Append("'");
                sql.Append(expression.Value);
                sql.Append("'");
            }
            else
            {
                sql.Append(expression.Value);
            }

            return expression;
        }
    }
}