using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Ast
{
    public class SelectColumn : SqlExpression
    {
        public SelectColumn(ColumnName column, string alias)
        {
            Column = column;
            Alias = alias;
        }

        public ColumnName Column { get; }
        public string Alias { get; }
    }
}