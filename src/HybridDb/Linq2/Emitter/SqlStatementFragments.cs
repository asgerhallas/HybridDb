using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Linq2.Emitter
{
    public class SqlStatementFragments
    {
        public SqlStatementFragments() { }

        public SqlStatementFragments(SqlStatementFragments fragments)
        {
            Select = fragments.Select;
            From = fragments.From;
            Where = fragments.Where;
            Take = fragments.Take;
            Skip = fragments.Skip;
            OrderBy = fragments.OrderBy;
            ParametersByValue = fragments.ParametersByValue;
            Aliases = fragments.Aliases;
        }

        public string Select { get; private set; } = "";
        public string From { get; private set; } = "";
        public string Where { get; private set; } = "";
        public int Take { get; private set; } = 0;
        public int Skip { get; private set; } = 0;
        public string OrderBy { get; private set; } = "";
        public IReadOnlyDictionary<object, string> ParametersByValue { get; private set; } = new Dictionary<object, string>();
        public IReadOnlyDictionary<string, string> Aliases { get; private set; } = new Dictionary<string, string>();
        public IDictionary<string, object> ParametersByName => ParametersByValue.ToDictionary(x => x.Value, x => x.Key);

        public SqlStatementFragments WriteSelect(EmitResult result) => new SqlStatementFragments(this)
        {
            Select = result.Sql,
            ParametersByValue = result.Parameters.ToDictionary(),
            Aliases = result.Aliases.ToDictionary()
        };

        public SqlStatementFragments WriteFrom(EmitResult result) => new SqlStatementFragments(this)
        {
            From = result.Sql,
            ParametersByValue = result.Parameters.ToDictionary(),
            Aliases = result.Aliases.ToDictionary()
        };

        public SqlStatementFragments WriteWhere(EmitResult result) => new SqlStatementFragments(this)
        {
            Where = result.Sql,
            ParametersByValue = result.Parameters.ToDictionary(),
            Aliases = result.Aliases.ToDictionary()
        };
    }
}