using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace HybridDb.Config
{
    public class CachedTypeMapper : ITypeMapper
    {
        readonly ITypeMapper typeMapper;
        readonly ConcurrentDictionary<(Type, string), Type> nameToType = new();
        readonly ConcurrentDictionary<Type, string> typeToName = new();

        public CachedTypeMapper(ITypeMapper typeMapper) => this.typeMapper = typeMapper;

        public void Add(Assembly assembly) => typeMapper.Add(assembly);

        public string ToDiscriminator(Type type) => 
            typeToName.GetOrAdd(type, key => typeMapper.ToDiscriminator(key));

        public Type ToType(Type basetype, string discriminator) => 
            nameToType.GetOrAdd((basetype, discriminator), _ => typeMapper.ToType(basetype, discriminator));
    }
}