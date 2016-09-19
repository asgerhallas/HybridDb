using System;
using System.Collections.Generic;

namespace HybridDb.Config
{
    public class Projection
    {
        Projection(Type returnType, Func<object, Dictionary<string, List<string>>, object> projector)
        {
            ReturnType = returnType;
            Projector = projector;
        }

        public static Projection From<TReturnType>(Func<object, Dictionary<string, List<string>>, object> projection)
        {
            return new Projection(typeof(TReturnType), projection);
        }

        public static Projection From<TReturnType>(Func<object, object> projection)
        {
            return new Projection(typeof(TReturnType), (document, metadata) => projection(document));
        }

        public Type ReturnType { get; private set; }
        public Func<object, Dictionary<string, List<string>>, object> Projector { get; private set; }
    }
}