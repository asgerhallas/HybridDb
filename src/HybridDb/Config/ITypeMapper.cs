using System;
using System.Reflection;

namespace HybridDb.Config
{
    public interface ITypeMapper
    {
        void Add(Assembly assembly) {}
        string ToDiscriminator(Type type);
        Type ToType(Type basetype, string discriminator);
    }
}