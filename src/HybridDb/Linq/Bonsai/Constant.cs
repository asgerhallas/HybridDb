using System;

namespace HybridDb.Linq.Bonsai
{
    public class Constant : BonsaiExpression
    {
        public Constant(object value, Type type) : base(type) => Value = value;

        public object Value { get; }
    }
}