using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace HybridDb
{
    public class ColumnNameBuilder : ExpressionVisitor
    {
        public ColumnNameBuilder()
        {
            ColumnName = "";
        }

        public string ColumnName { get; private set; }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            Visit(node.Left);
            ColumnName += node.NodeType;
            Visit(node.Right);
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // If it is an extension method, reverse the notation
            if (node.Method.IsDefined(typeof (ExtensionAttribute), true))
            {
                Visit(node.Arguments[0]);
                ColumnName += node.Method.Name;
                foreach (var args in node.Arguments.Skip(1))
                {
                    Visit(args);
                }
            }
            else
            {
                if (node.Object != null)
                {
                    Visit(node.Object);
                }
                else
                {
                    ColumnName += node.Method.DeclaringType.Name;
                }
                
                ColumnName += node.Method.Name;
                foreach (var args in node.Arguments)
                {
                    Visit(args);
                }
            }
            
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null)
            {
                Visit(node.Expression);
            }
            else
            {
                ColumnName += node.Member.DeclaringType.Name;
            }

            ColumnName += node.Member.Name;
            
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            ColumnName += node.Value.ToString();
            return node;
        }
    }
}