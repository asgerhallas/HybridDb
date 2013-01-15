using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    internal class ParameterMemberVisitor : ExpressionVisitor
    {
        string columnName;
        SqlExpression sqlExpression;

        public static SqlExpression Translate(Expression expression)
        {
            var visitor = new ParameterMemberVisitor();
            visitor.Visit(expression);
            return visitor.sqlExpression ?? new SqlExpression();
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression is ParameterExpression)
            {
                sqlExpression = new SqlColumnExpression(columnName);
            }
            else
            {
                columnName += expression.Member.Name;
                Visit(expression.Expression);
            }

            return expression;
        }
    }
}