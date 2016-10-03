using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Linq;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;
using ShinySwitch;

namespace HybridDb.Linq2
{
    public class SqlStatementEmitter
    {
        public SqlSelectStatement Emit(SelectStatement query)
        {
            var statement = new SqlSelectStatement();

            var parameters = new Dictionary<object, string>();

            statement.Select = EmitSelect(query.Select, parameters);

            var result = EmitWhere(query.Where, parameters);
            statement.Where = result.Sql;
            statement.Parameters = result.Parameters.ToDictionary(x => x.Value, x => x.Key);

            return statement;
        }

        string EmitSelect(Select select, IDictionary<object, string> parameters)
        {
            return "*";
        }

        Result EmitWhere(Where where, IDictionary<object, string> parameters)
        {
            return Emit(new Result(), @where.Predicate);
        }

        public static Result Emit(Result result, Expression expression)
        {
            var emit = Switch<Result>.On(expression)
                .Match<ColumnIdentifier>(x => result.Append(x.Name))
                .Match<Constant>(x => EmitConstant(result, x.Value))
                .Match<Comparison>(x => result
                    .Emit(x.Left)
                    .Append(Switch<string>.On(x.Operator)
                        .Match(ComparisonOperator.Equal, "=")
                        .Match(ComparisonOperator.NotEqual, "<>")
                        .Match(ComparisonOperator.GreaterThan, "=")
                        .Match(ComparisonOperator.GreaterThanOrEqualTo, "=")
                        .Match(ComparisonOperator.LessThan, "=")
                        .Match(ComparisonOperator.LessThenOrEqualTo, "="))
                    .Emit(x.Right))
                .OrThrow(new ArgumentException($"Unknown expresion type '{expression}'."));
            return emit;
        }

        static Result EmitConstant(Result result, object value)
        {
            return Switch<Result>.On(value)
                .Match<object>(value == null, _ => new Result("NULL"))
                .Match<IEnumerable>(!(value is string), v =>
                {
                    var listOfValues = v.Cast<object>().ToList();

                    return listOfValues.Count != 0
                        ? listOfValues.Aggregate((Result) null,
                            (acc, next) => acc == null
                                ? result.Append(EmitConstant(result, next))
                                : acc.Append(", ").Append(EmitConstant(acc, next)))
                        : new Result("NULL");
                })
                .Else(() =>
                {
                    var parameterKey = "@Value" + result.Parameters.Count;

                    return new Result(new Dictionary<object, string> {[value] = parameterKey}, parameterKey);
                });
        }


        public class Result
        {
            public Result() : this(new Dictionary<object, string>(), "") {}

            public Result(string sql) : this(new Dictionary<object, string>(), sql) {}

            public Result(IReadOnlyDictionary<object, string> parameters, string sql)
            {
                Parameters = parameters;
                Sql = sql;
            }

            public IReadOnlyDictionary<object, string> Parameters { get; }
            public string Sql { get; }

            public Result Append(string sql)
            {
                return new Result(Parameters, Sql + sql);
            }

            public Result Append(Result result)
            {
                return new Result(Parameters.Concat(result.Parameters).ToDictionary(), Sql + result.Sql);
            }
        }
    }
}