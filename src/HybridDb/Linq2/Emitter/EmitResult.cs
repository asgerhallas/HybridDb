using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Linq2.Emitter
{
    public class EmitResult
    {
        EmitResult(string sql, IReadOnlyDictionary<object, string> parameters, IReadOnlyDictionary<string, string> aliases)
        {
            Sql = sql;
            Parameters = parameters;
            Aliases = aliases;
        }

        public string Sql { get; }
        public IReadOnlyDictionary<object, string> Parameters { get; }
        public IReadOnlyDictionary<string, string> Aliases { get; }

        public static EmitResult New()
        {
            return New("");
        }

        public static EmitResult New(string sql)
        {
            return new EmitResult(sql, new Dictionary<object, string>(), new Dictionary<string, string>());
        }

        public static EmitResult New(IReadOnlyDictionary<object, string> paramtersByValue)
        {
            return new EmitResult("", paramtersByValue, new Dictionary<string, string>());
        }

        public static EmitResult New(IReadOnlyDictionary<string, string> aliases)
        {
            return new EmitResult("", new Dictionary<object, string>(), aliases);
        }

        public EmitResult Append(string sql)
        {
            return new EmitResult(Sql + sql, Parameters, Aliases);
        }

        public EmitResult Append(EmitResult result)
        {
            return new EmitResult(
                Sql + result.Sql, 
                Parameters.Concat(result.Parameters).ToDictionary(), 
                Aliases.Concat(result.Aliases).ToDictionary());
        }

        public EmitResult AddParameter(object value, string key)
        {
            var pairs = new List<KeyValuePair<object, string>> { new KeyValuePair<object, string>(value, key) };
            return new EmitResult(Sql, Parameters.Concat(pairs).ToDictionary(), Aliases);
        }

        public EmitResult AddAlias(string identifier, string alias)
        {
            var pairs = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(identifier, alias) };
            return new EmitResult(Sql, Parameters, Aliases.Concat(pairs).ToDictionary());
        }
    }
}