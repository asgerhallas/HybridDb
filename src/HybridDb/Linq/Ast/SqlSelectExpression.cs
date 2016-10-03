using System.Collections.Generic;

namespace HybridDb.Linq.Ast
{
    public class SqlSelectExpression : SqlExpression
    {
        public SqlSelectExpression(IEnumerable<SqlProjectionExpression> projections)
        {
            Projections = projections;
        }

        public IEnumerable<SqlProjectionExpression> Projections { get; private set; }
    }
}