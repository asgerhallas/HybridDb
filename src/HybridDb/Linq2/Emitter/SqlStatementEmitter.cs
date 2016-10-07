using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;
using ShinySwitch;

namespace HybridDb.Linq2.Emitter
{
    public class SqlStatementEmitter
    {
        public SqlStatementFragments Emit(SelectStatement query)
        {
            return query.Clauses.Aggregate(new SqlStatementFragments(), (acc, clause) =>
                Switch<SqlStatementFragments>.On(clause)
                    .Match<From>(x => acc)
                    .Match<Select>(x => acc.WriteSelect(x.Selects.Aggregate(EmitResult.Empty().Append(acc.ParametersByValue), Emit)))
                    .Match<Where>(x => acc.WriteWhere(Emit(EmitResult.Empty(), x.Predicate)))
                    .OrThrow());
        }

        public static EmitResult Emit(EmitResult result, AstNode expression)
        {
            var emit = Switch<EmitResult>.On(expression)
                .Match<Where>(x => result.Emit(x.Predicate))
                .Match<ColumnIdentifier>(x => result.Append(x.ColumnName))
                .Match<Constant>(x => EmitConstant(result, x.Value))
                .Match<IBinaryOperator>(x => result
                    .Append("(").Emit(x.Left)
                    .Append(Switch<string>.On(expression)
                        .Match<Comparison>(c => Switch<string>.On(c.Operator)
                            .Match(ComparisonOperator.Equal, " = ")
                            .Match(ComparisonOperator.NotEqual, " <> ")
                            .Match(ComparisonOperator.GreaterThan, " > ")
                            .Match(ComparisonOperator.GreaterThanOrEqualTo, " >= ")
                            .Match(ComparisonOperator.LessThan, " < ")
                            .Match(ComparisonOperator.LessThenOrEqualTo, " <= "))
                        .Match<Logical>(c => Switch<string>.On(c.Operator)
                            .Match(LogicalOperator.And, " AND ")
                            .Match(LogicalOperator.Or, " OR "))
                        .Match<Bitwise>(c => Switch<string>.On(c.Operator)
                            .Match(BitwiseOperator.And, " & ")
                            .Match(BitwiseOperator.Or, " | ")))
                    .Emit(x.Right).Append(")"))
                .Match<Is>(x => result
                    .Append("(").Emit(x.Operand)
                    .Append(" IS")
                    .Append(x.Case == NullNotNull.NotNull ? " NOT" : "")
                    .Append(" NULL)"))
                .OrThrow(new ArgumentException($"Unknown expresion type '{expression}'."));

            return emit;
        }

        static EmitResult EmitConstant(EmitResult result, object value)
        {
            return Switch<EmitResult>.On(value)
                .Match(null, _ => result.Append("NULL"))
                .Match<IEnumerable>(!(value is string), v =>
                {
                    var listOfValues = v.Cast<object>().ToList();

                    return listOfValues.Count != 0
                        ? listOfValues.Aggregate((EmitResult) null,
                            (acc, next) => acc == null
                                ? EmitConstant(result, next)
                                : acc.Append(", ").Append(EmitConstant(acc, next)))
                        : result.Append("NULL");
                })
                .Else(() =>
                {
                    var parameterKey = "@Value" + result.Parameters.Count;

                    return result.Append(parameterKey).Append(new Dictionary<object, string> {[value] = parameterKey});
                });
        }
    }
}