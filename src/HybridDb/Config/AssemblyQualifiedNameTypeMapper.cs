using System;
using System.Collections.Concurrent;

namespace HybridDb.Config
{
    public class AssemblyQualifiedNameTypeMapper : TypeMapper
    {
        public override string ToDiscriminator(Type type) => type.AssemblyQualifiedName;
    }
}