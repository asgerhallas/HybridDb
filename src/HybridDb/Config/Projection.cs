using System;

namespace HybridDb.Config
{
    public class Projection
    {
        Projection(Type returnType, Func<ManagedEntity, object> projector)
        {
            ReturnType = returnType;
            Projector = projector;
        }

        public static Projection From<TReturnType>(Func<ManagedEntity, object> projection)
        {
            return new Projection(typeof(TReturnType), projection);
        }

        public Type ReturnType { get; private set; }
        public Func<ManagedEntity, object> Projector { get; private set; }
    }
}