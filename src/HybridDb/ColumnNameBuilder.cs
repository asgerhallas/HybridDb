using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace HybridDb
{
    public class ColumnNameBuilder : ExpressionVisitor
    {
        readonly Expression projector;

        public ColumnNameBuilder(Expression projector)
        {
            this.projector = projector;
            ColumnName = "";
        }

        string ColumnName { get; set; }

        public static string GetColumnNameByConventionFor(Expression projector)
        {
            var columnNameBuilder = new ColumnNameBuilder(projector);
            columnNameBuilder.Visit(projector);
            return columnNameBuilder.ColumnName;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.IsDefined(typeof(ExtensionAttribute), true))
            {
                if (node.Arguments.Skip(1).Any(x => !(x is ConstantExpression)))
                {
                    throw new HybridDbException($"Projection '{projector}' is to complex to name by convention. Please define a columnn name manually.");
                }

                Visit(node.Arguments[0]);
            }
            else
            {
                if (node.Arguments.Any(x => !(x is ConstantExpression)))
                {
                    throw new HybridDbException($"Projection '{projector}' is to complex to name by convention. Please define a columnn name manually.");
                }

                Visit(node.Object);
            }

            ColumnName += node.Method.Name;

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null)
            {
                Visit(node.Expression);
            }

            ColumnName += node.Member.Name;
            
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            ColumnName += Regex.Replace(node.Value.ToString(), "[^a-zA-Z0-9]", "");
            return node;
        }
    }
}