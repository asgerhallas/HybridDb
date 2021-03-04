using System;

namespace HybridDb.Config
{
    public class ShortNameTypeMapper : TypeMapper
    {
        public override string ToDiscriminator(Type type) =>
            type.IsNested
                ? $"{type.DeclaringType.Name}+{type.Name}"
                : type.Name;
    }
}