using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb.Linq.Old
{
    public static class ExpressionEx
    {
        public static MemberExpressionInfo GetMemberExpressionInfo(this Expression expression)
        {
            if (expression == null)
                return null;

            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                var memberExpression = ((MemberExpression)expression);
                var name = memberExpression.Expression.GetMemberExpressionInfo();
                if (name == null)
                    return null;

                return new MemberExpressionInfo
                {
                    FullName = name.FullName + memberExpression.Member.Name,
                    ResultingType = memberExpression.Member.GetMemberType()
                };
            }

            if (expression.NodeType == ExpressionType.Parameter)
            {
                return new MemberExpressionInfo();
            }

            return null;
        }

        public class MemberExpressionInfo
        {
            public MemberExpressionInfo()
            {
                FullName = "";
            }

            public string FullName { get; set; }
            public MemberInfo ResultingType { get; set; }
        }
    }
}