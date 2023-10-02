using System.Collections.Generic;

namespace HybridDb.Linq.Old.Ast
{
    public class SqlOrderByExpression : SqlExpression
    {
        public SqlOrderByExpression(IEnumerable<SqlOrderingExpression> columns) => Columns = columns;

        public IEnumerable<SqlOrderingExpression> Columns { get; private set; }

        public override SqlNodeType NodeType => SqlNodeType.OrderBy;
    }
}