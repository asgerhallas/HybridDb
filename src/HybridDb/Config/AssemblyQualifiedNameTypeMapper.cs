using System;

namespace HybridDb.Config
{
    public class AssemblyQualifiedNameTypeMapper : ITypeMapper
    {
        public string ToDiscriminator(Type type) => type.AssemblyQualifiedName;

        public Type ToType(Type basetype, string discriminator)
        {
            var type = Type.GetType(discriminator, throwOnError: true); 
            
            return basetype.IsAssignableFrom(type) 
                ? type 
                : throw new InvalidOperationException($"Discriminated {type} is not assignable to {basetype}.");
        }
    }
}