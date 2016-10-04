using System.Collections.Generic;
using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Ast
{
    public class OrderBy : SqlClause
    {
        public OrderBy(IEnumerable<OrderByExpression> columns)
        {
            Columns = columns;
        }

        public IEnumerable<OrderByExpression> Columns { get; private set; }
    }
}