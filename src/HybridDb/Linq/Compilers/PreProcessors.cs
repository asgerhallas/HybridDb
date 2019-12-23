using System;
using System.Linq.Expressions;
using System.Reflection;
using HybridDb.Linq.Plugins;
using ShinySwitch;

namespace HybridDb.Linq.Compilers
{
    public static class PreProcessors
    {
        public static Func<Expression, Expression> All => exp => PropagateImplicitBooleans(ReduceConstantMemberAccess(exp));

        /// <summary>
        /// Reduce member access - or chains of member accesses - on constants to an evaluated constant.
        /// Member access on a constant is expected to be constant for the duration of the query.
        /// If you reference non-pure members on a constant in a query, the value is snapshot at compile-time and not evaluated at each invokation of the query.
        /// </summary>
        public static Expression ReduceConstantMemberAccess(Expression exp) => Switch<Expression>.On(exp)
            .Match<MemberExpression>(member => Switch<Expression>.On(ReduceConstantMemberAccess(member.Expression)) // go deep first, follow the member chain to its root
                .Match<ConstantExpression>(constant => // which is either a constant, in which case we rewrite the member access on that constant to a new constant
                {
                    var value = Switch<(object value, Type type)>.On(member.Member)
                        .Match<FieldInfo>(m => (m.GetValue(constant.Value), m.FieldType))
                        .Match<PropertyInfo>(m => (m.GetValue(constant.Value), m.PropertyType))
                        .OrThrow();

                    return Expression.Constant(value.value, value.type);
                })
                .Else(member)) // or something else - like a parameter - in which case this is not constant and shouldn't be reduced
            .Else(Visitor.Continue(ReduceConstantMemberAccess, exp));

        public static Expression PropagateImplicitBooleans(Expression exp) => Switch<Expression>.On(exp)
            .Match<MemberExpression>(x => x.Member.Type() == typeof(bool)
                ? Expression.MakeBinary(ExpressionType.Equal, x, Expression.Constant(true))
                : (Expression) x)
            .Match<BinaryExpression>(x => !x.NodeType.In(ExpressionType.AndAlso, ExpressionType.OrElse), x => x)
                //Expression.MakeBinary(x.NodeType, Visitor.)
                //Visitor.Continue(PropagateImplicitBooleans, x))
            .Else(Visitor.Continue(PropagateImplicitBooleans, exp));
    }
}