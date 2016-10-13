using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;
using ShinySwitch;

namespace HybridDb.Linq2.Emitter
{
    //todo:gør det muligt at lave en standard visitor, der tager et override med ind

    public class SqlStatementEmitter
    {
        readonly Func<string, string> formatAndEscapeTableName;

        public SqlStatementEmitter(Func<string, string> formatAndEscapeTableName)
        {
            this.formatAndEscapeTableName = formatAndEscapeTableName;
        }

        public SqlStatementFragments Emit(SelectStatement query)
        {
            return query.Clauses.Aggregate(new SqlStatementFragments(), (acc, clause) =>
                Switch<SqlStatementFragments>.On(clause)
                    .Match<From>(x => acc.WriteFrom(x.Joins.Aggregate(EmitResult.New(acc.ParametersByValue).Apply(Emit, x.Table), Emit)))
                    .Match<Select>(x => x.SelectList.Any() 
                        ? acc.WriteSelect(EmitResult.New()
                            .Apply(Emit, x.SelectList[0])
                            .Apply(JoinEmit, ", ", x.SelectList.Skip(1))) 
                        : acc)
                    .Match<Where>(x => acc.WriteWhere(Emit(EmitResult.New(acc.ParametersByValue), x.Condition))) //Todo: aliaese med i where
                    .OrThrow());
        }

        public EmitResult Emit(EmitResult result, AstNode expression)
        {
            var emit = Switch<EmitResult>.On(expression)
                .Match<TableName>(x => result.Append(formatAndEscapeTableName(x.Name))) //TODO: Er tablename relevant at have som sin egen ast?
                .Match<Join>(x => result
                    .Append(" JOIN ").Apply(Emit, x.Table)
                    .Append(" ON ").Apply(Emit, x.Condition))
                .Match<Where>(x => result.Apply(Emit, x.Condition))
                .Match<SelectColumn>(x =>
                {
                    var fullyQualifiedTableName = FullyQualifiedColumnName(x.Column);

                    return result
                        .Append(fullyQualifiedTableName)
                        .Append(" AS ")
                        .Append(x.Alias)
                        .AddAlias(fullyQualifiedTableName, x.Alias);
                })
                .Match<ColumnName>(x =>
                {
                    var fullyQualifiedColumnName = FullyQualifiedColumnName(x);

                    string alias;
                    return result.Append(
                        !result.Aliases.TryGetValue(fullyQualifiedColumnName, out alias)
                            ? fullyQualifiedColumnName
                            : alias);
                })
                .Match<True>(x => result.Append("(1=1)"))
                .Match<False>(x => result.Append("(1<>1)"))
                .Match<Constant>(x => EmitConstant(result, x.Value))
                .Match<IBinaryOperator>(x => result
                    .Append("(").Apply(Emit, x.Left)
                    .Append(Switch<string>.On(expression)
                        .Match<Comparison>(c => Switch<string>.On(c.Operator)
                            .Match(ComparisonOperator.Equal, " = ")
                            .Match(ComparisonOperator.NotEqual, " <> ")
                            .Match(ComparisonOperator.GreaterThan, " > ")
                            .Match(ComparisonOperator.GreaterThanOrEqualTo, " >= ")
                            .Match(ComparisonOperator.LessThan, " < ")
                            .Match(ComparisonOperator.LessThenOrEqualTo, " <= "))
                        .Match<Logic>(c => Switch<string>.On(c.Operator)
                            .Match(LogicOperator.And, " AND ")
                            .Match(LogicOperator.Or, " OR "))
                        .Match<Bitwise>(c => Switch<string>.On(c.Operator)
                            .Match(BitwiseOperator.And, " & ")
                            .Match(BitwiseOperator.Or, " | ")))
                    .Apply(Emit, x.Right).Append(")"))
                .Match<Is>(x => result
                    .Append("(").Apply(Emit, x.Operand)
                    .Append(" IS")
                    .Append(x.Case == NullNotNull.NotNull ? " NOT" : "")
                    .Append(" NULL)"))
                .Match<Like>(x => result
                    .Append("(").Apply(Emit, x.Left).Append(" LIKE '")
                    .Append(x.Pattern.Aggregate(EmitResult.New(result.Parameters.ToDictionary()), EmitLikePattern))
                    .Append("')"))
                .OrThrow(new ArgumentException($"Unknown expresion type '{expression}'."));

            return emit;
        }

        static EmitResult EmitLikePattern(EmitResult result, Either<Constant, Wildcard> next)
        {
            return Switch<EmitResult>.On(next)
                .Match<A<Wildcard>>(w => result.Append(Switch<string>.On(w.Value.Operator)
                    .Match(WildcardOperator.OneOrMore, "%")))
                .Match<A<Constant>>(c => result.Append("' + ").Apply(EmitConstant, c.Value.Value).Append(" + '"));
        }

        static EmitResult EmitConstant(EmitResult result, object value)
        {
            return Switch<EmitResult>.On(value)
                .Match(null, _ => result.Append("NULL"))
                .Match<IEnumerable>(_ => !(value is string), v =>
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
                    string parameterKey;
                    if (!result.Parameters.TryGetValue(value, out parameterKey))
                    {
                        parameterKey = "@Value" + result.Parameters.Count;
                        return result.Append(parameterKey).AddParameter(value, parameterKey);
                    }

                    return result.Append(parameterKey);
                });
        }

        string FullyQualifiedColumnName(ColumnName column)
        {
            return $"{formatAndEscapeTableName(column.TableName)}.[{column.Identifier}]";
        }

        EmitResult JoinEmit(EmitResult result, string delimiter, IEnumerable<AstNode> columns)
        {
            return columns.Aggregate(result, (a, b) => a.Append(delimiter).Apply(Emit, b));
        }
    }
}