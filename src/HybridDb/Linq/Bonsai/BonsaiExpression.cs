using System;

namespace HybridDb.Linq.Bonsai
{
    public abstract class BonsaiExpression
    {
        protected BonsaiExpression(Type type) => Type = type;

        public Type Type { get; }

        public T AssertNotNull<T>(T value) where T : class => value ?? throw new ArgumentNullException();
    }
}