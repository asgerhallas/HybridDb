using System.Collections.Generic;

namespace HybridDb.Linq.Old.Ast
{
    public class SqlSelectExpression : SqlExpression
    {
        public SqlSelectExpression(IEnumerable<SqlProjectionExpression> projections)
        {
            Projections = projections;
        }

        public IEnumerable<SqlProjectionExpression> Projections { get; private set; }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Select; }
        }
    }
}