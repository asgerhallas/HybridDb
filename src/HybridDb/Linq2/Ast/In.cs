using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Linq2.Ast
{
    public class In : Predicate
    {
        public In(SqlExpression left, params SqlExpression[] expressions)
        {
            Left = left;
            SubQueryOrExpressions = new Either<IReadOnlyList<SqlExpression>, SelectStatement>(expressions.ToList());
        }

        public In(SqlExpression left, SelectStatement subquery)
        {
            Left = left;
            SubQueryOrExpressions = new Either<IReadOnlyList<SqlExpression>, SelectStatement>(subquery);
        }

        public SqlExpression Left { get; }
        public Either<IReadOnlyList<SqlExpression>, SelectStatement> SubQueryOrExpressions { get; }
    }
}