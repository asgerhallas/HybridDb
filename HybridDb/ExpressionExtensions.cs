using System;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb
{
    public static class ExpressionExtensions
    {
        public static PropertyInfo GetPropertyInfo<T>(this Expression<Func<T, object>> expression)
        {
            return GetPropertyInfo((LambdaExpression) expression);
        }

        public static PropertyInfo GetPropertyInfo(this LambdaExpression expression)
        {
            return (PropertyInfo) GetPropertyOrFieldExpression(expression).Member;
        }

        public static FieldInfo GetFieldInfo(this LambdaExpression expression)
        {
            return (FieldInfo) GetPropertyOrFieldExpression(expression).Member;
        }

        public static MemberExpression GetPropertyOrFieldExpression(this LambdaExpression expression)
        {
            var member = expression.GetMemberExpression();
            if (member.NodeType != ExpressionType.MemberAccess)
                throw new ArgumentException("Selected member must be a property or a field");

            return member;
        }

        public static MemberExpression GetMemberExpression(this LambdaExpression expression)
        {
            if (expression == null)
                return null;

            MemberExpression me;
            switch (expression.Body.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    var ue = expression.Body as UnaryExpression;
                    me = ((ue != null) ? ue.Operand : null) as MemberExpression;
                    break;
                default:
                    me = expression.Body as MemberExpression;
                    break;
            }

            if (me == null)
                throw new ArgumentException("Expression is not a member expression");

            return me;
        }
    }
}