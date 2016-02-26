using System;

namespace HybridDb.Config
{
    public class AssemblyQualifiedNameTypeMapper : ITypeMapper
    {
        public string ToDiscriminator(Type type)
        {
            return type.AssemblyQualifiedName;
        }

        public Type ToType(string discriminator)
        {
            return Type.GetType(discriminator, false, true);
        }
    }
}