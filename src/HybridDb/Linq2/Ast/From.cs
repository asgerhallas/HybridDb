using System.Collections.Generic;

namespace HybridDb.Linq2.Ast
{
    public class From : SqlClause
    {
        public From(TableName table, params Join[] joins)
        {
            Table = table;
            Joins = joins;
        }

        public TableName Table { get; }
        public IReadOnlyList<Join> Joins { get; }
    }
}