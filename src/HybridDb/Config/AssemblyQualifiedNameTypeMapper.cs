using System;

namespace HybridDb.Config
{
    public class AssemblyQualifiedNameTypeMapper : ITypeMapper
    {
        public string ToDiscriminator(Type type) => type.AssemblyQualifiedName;
        public Type ToType(string discriminator) => Type.GetType(discriminator, false, true);
    }
}