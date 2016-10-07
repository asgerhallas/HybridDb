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
            Where = fragments.Where;
            Take = fragments.Take;
            Skip = fragments.Skip;
            OrderBy = fragments.OrderBy;
            ParametersByValue = fragments.ParametersByValue;
        }

        public string Select { get; private set; } = "";
        public string Where { get; private set; } = "";
        public int Take { get; private set; } = 0;
        public int Skip { get; private set; } = 0;
        public string OrderBy { get; private set; } = "";
        public IReadOnlyDictionary<object, string> ParametersByValue { get; private set; } = new Dictionary<object, string>();
        public IDictionary<string, object> ParametersByName => ParametersByValue.ToDictionary(x => x.Value, x => x.Key);

        public SqlStatementFragments WriteSelect(EmitResult result) => new SqlStatementFragments
        {
            Select = result.Sql,
            ParametersByValue = ParametersByValue.Concat(result.Parameters).ToDictionary()
        };

        public SqlStatementFragments WriteWhere(EmitResult result) => new SqlStatementFragments
        {
            Where = result.Sql,
            ParametersByValue = ParametersByValue.Concat(result.Parameters).ToDictionary()
        };
    }
}