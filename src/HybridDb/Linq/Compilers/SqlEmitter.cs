using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HybridDb.Linq.Bonsai;
using ShinySwitch;

namespace HybridDb.Linq.Compilers
{
    public static class SqlEmitter
    {
        static readonly IReadOnlyDictionary<string, string> metadataColumns = new Dictionary<string, string>
        {
            [nameof(View.Key)] = "Id" // TODO: is this string a part of hybrid?
        };

        public static string Emit(BonsaiExpression exp, Emitter top, Emitter next) => Switch<string>.On(exp)
            .Match<Constant>(constant =>
            {
                if (constant.Type.IsEnum)
                {
                    return $"'{Enum.GetName(constant.Type, constant.Value)}'";
                }

                if (constant.Type == typeof(string))
                {
                    return $"'{constant.Value}'";
                }

                if (constant.Type.IsNumericType())
                {
                    return (string) Convert.ChangeType(constant.Value, typeof(string), CultureInfo.InvariantCulture);
                }

                if (constant.Value is bool boolean)
                {
                    return boolean ? "1" : "0";
                }

                throw new ArgumentOutOfRangeException($"Constant of type {constant.Type} cannot be emitted.");
            })
            .Match<List>(list => string.Join(", ", list.Values.Select(top.Invoke)))
            .Match<Column>(column => column.IsMetadata ? metadataColumns[column.Name] : column.Name)
            .Match<BinaryLogic>(logic =>
            {
                var left = top(logic.Left);
                var right = top(logic.Right);

                var @operator = Switch<string>.On(logic.Operator)
                    .Match(BinaryLogicOperator.AndAlso, "AND")
                    .Match(BinaryLogicOperator.OrElse, "OR")
                    .OrThrow();

                return $"{left} {@operator} {right}";
            })
            .Match<UnaryLogic>(logic => Switch<string>.On(logic.Operator)
                .Match(UnaryLogicOperator.Not, _ => $"NOT ({top(logic.Expression)})")
                .OrThrow())
            .Match<Comparison>(comparison =>
            {
                var left = top(comparison.Left);
                var right = top(comparison.Right);

                var @operator = Switch<string>.On(comparison.Operator)
                    .Match(ComparisonOperator.Equal, "=")
                    .Match(ComparisonOperator.NotEqual, "!=")
                    .Match(ComparisonOperator.GreaterThan, ">")
                    .Match(ComparisonOperator.GreaterThanOrEqualTo, ">=")
                    .Match(ComparisonOperator.LessThan, "<")
                    .Match(ComparisonOperator.LessThanOrEqualTo, "<=")
                    .OrThrow();

                return $"{left} {@operator} {right}";
            })
            .OrThrow();

        public static bool IsNumericType(this Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }
}