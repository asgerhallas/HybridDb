using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HybridDb.Linq.Bonsai;
using ShinySwitch;

namespace HybridDb.Linq.Compilers
{
    public static class RootCompiler
    {
        public static BonsaiExpression Compile(Expression exp, Compiler top, Compiler next) => Switch<BonsaiExpression>.On(exp)
            .Match<LambdaExpression>(x => top(x.Body))
            .Match<UnaryExpression>(x => Switch<BonsaiExpression>.On(x.NodeType)
                .Match(ExpressionType.Convert, _ => top(x.Operand))
                .Match(ExpressionType.Not, _ => new UnaryLogic(UnaryLogicOperator.Not, top(x.Operand)))
                .OrThrow())
            .Match<BinaryExpression>(x =>
            {
                var left = top(x.Left);
                var right = top(x.Right);

                return Switch<BonsaiExpression>.On(x.NodeType)
                    .Match(ExpressionType.Equal, _ => new Comparison(ComparisonOperator.Equal, left, right))
                    .Match(ExpressionType.NotEqual, _ => new Comparison(ComparisonOperator.NotEqual, left, right))
                    .Match(ExpressionType.LessThan, _ => new Comparison(ComparisonOperator.LessThan, left, right))
                    .Match(ExpressionType.LessThanOrEqual, _ => new Comparison(ComparisonOperator.LessThanOrEqualTo, left, right))
                    .Match(ExpressionType.GreaterThan, _ => new Comparison(ComparisonOperator.GreaterThan, left, right))
                    .Match(ExpressionType.GreaterThanOrEqual, _ => new Comparison(ComparisonOperator.GreaterThanOrEqualTo, left, right))
                    .Match(ExpressionType.AndAlso, _ => new BinaryLogic(BinaryLogicOperator.AndAlso, left, right))
                    .Match(ExpressionType.OrElse, _ => new BinaryLogic(BinaryLogicOperator.OrElse, left, right))
                    .OrThrow();
            })
            .Match<ConstantExpression>(x => MakeConstant(x.Value, x.Type))
            .Match<MemberExpression>(x => Switch<BonsaiExpression>.On(x.Expression)
                .Match<ParameterExpression>(_ => new Column(x.Member.Name, x.Member.DeclaringType == typeof(View), x.Member.Type())) // TODO: lookinto IsMetadata
                .Match<ConstantExpression>(constant => MakeConstant(((FieldInfo) x.Member).GetValue(constant.Value), x.Member.Type()))
                .Else(() => next(x)))
            .Match<NewArrayExpression>(x => new List(x.Expressions.Select(top.Invoke), x.Type.GetElementTypeOfEnumerable(), x.Type))
            .OrThrow();

        public static BonsaiExpression PostProcess(BonsaiExpression expression, PostProcessor top, PostProcessor next) =>
            Switch<BonsaiExpression>.On(expression)
                .Match<BinaryLogic>(x => new BinaryLogic(x.Operator, top(x.Left), top(x.Right)))
                .Match<Column>(x => x)
                .Match<Comparison>(x => new Comparison(x.Operator, top(x.Left), top(x.Right)))
                .Match<Constant>(x => x)
                .Match<UnaryLogic>(x => new UnaryLogic(x.Operator, top(x.Expression)))
                .Match<List>(x => new List(x.Values.Select(top.Invoke), x.ElementType, x.Type))
                .OrThrow();

        static BonsaiExpression MakeConstant(object value, Type type)
        {
            if (!(value is IEnumerable<object> enumerable)) return new Constant(value, value?.GetType() ?? type);

            var elementType = type.GetElementTypeOfEnumerable();

            return new List(enumerable.Select(x => new Constant(x, x.GetType())), elementType, type);
        }

        public static Type GetElementTypeOfEnumerable(this Type type)
        {
            if (type.IsArray) return type.GetElementType();

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return type.GetGenericArguments()[0];
            }

            var elementType = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(i => i.GetGenericArguments()[0])
                .FirstOrDefault();

            return elementType;
        }

        public static Type Type(this MemberInfo info) => Switch<Type>.On(info)
            .Match<FieldInfo>(x => x.FieldType)
            .Match<PropertyInfo>(x => x.PropertyType)
            .OrThrow();
    }
}