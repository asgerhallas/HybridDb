using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Linq2.Ast
{
    public class Select : Clause
    {
        public Select(params Projection[] projections)
        {
            Projections = projections.ToList();
        }

        public IReadOnlyList<Projection> Projections { get; }
    }
}