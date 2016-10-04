using System.Collections.Generic;
using System.Linq;
using HybridDb.Linq.Ast;

namespace HybridDb.Linq2.Ast
{
    public class Select : SqlClause
    {
        public Select(params SelectExpression[] selects)
        {
            Selects = selects.ToList();
        }

        public IReadOnlyList<SelectExpression> Selects { get; }
    }
}