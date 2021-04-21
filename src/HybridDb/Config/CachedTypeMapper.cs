using System;
using System.Collections.Concurrent;

namespace HybridDb.Config
{
    public class CachedTypeMapper : ITypeMapper
    {
        readonly ITypeMapper typeMapper;
        readonly ConcurrentDictionary<string, Type> nameToType = new();
        readonly ConcurrentDictionary<Type, string> typeToName = new();

        public CachedTypeMapper(ITypeMapper typeMapper) => this.typeMapper = typeMapper;

        public string ToDiscriminator(Type type) => typeToName.GetOrAdd(type, key => typeMapper.ToDiscriminator(key));

        public Type ToType(string discriminator) => nameToType.GetOrAdd(discriminator, key => typeMapper.ToType(key));
    }
}