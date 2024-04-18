using System.Linq.Expressions;

namespace HybridDb
{
    public class ColumnNameBuilder2 : ExpressionVisitor
    {
        string ColumnName { get; set; } = "";

        public static string GetColumnNameByConventionFor(Expression projector)
        {
            var columnNameBuilder = new ColumnNameBuilder2();
            columnNameBuilder.Visit(projector);
            return columnNameBuilder.ColumnName;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == null) return node;

            Visit(node.Expression);

            ColumnName += node.Member.Name;

            return node;
        }
    }
}