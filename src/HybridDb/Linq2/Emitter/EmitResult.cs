using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Linq2.Emitter
{
    public class EmitResult
    {
        EmitResult(string sql, IReadOnlyDictionary<object, string> parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }

        public string Sql { get; }
        public IReadOnlyDictionary<object, string> Parameters { get; }

        public static EmitResult Empty()
        {
            return new EmitResult("", new Dictionary<object, string>());
        }

        public EmitResult Append(string sql)
        {
            return new EmitResult(Sql + sql, Parameters);
        }

        public EmitResult Append(IReadOnlyDictionary<object, string> parameters)
        {
            return new EmitResult(Sql, Parameters.Concat(parameters).ToDictionary());
        }

        public EmitResult Append(EmitResult result)
        {
            return new EmitResult(Sql + result.Sql, Parameters.Concat(result.Parameters).ToDictionary());
        }
    }
}