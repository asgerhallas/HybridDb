using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Linq.Bonsai
{
    public class List : BonsaiExpression
    {
        public List(IEnumerable<BonsaiExpression> values, Type elementType, Type type) : base(type)
        {
            Values = values.ToList();
            ElementType = elementType;
        }

        public IReadOnlyList<BonsaiExpression> Values { get; }
        public Type ElementType { get; }
    }
}