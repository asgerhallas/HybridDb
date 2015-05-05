using System;
using System.Collections.Concurrent;
using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization.JsonNet
{
    public class CachingContractResolverDecorator : ExtendedContractResolver
    {
        readonly IContractResolver resolver;
        readonly ConcurrentDictionary<Type, JsonContract> contracts = new ConcurrentDictionary<Type, JsonContract>();

        public CachingContractResolverDecorator(IContractResolver resolver)
        {
            this.resolver = resolver;
        }

        public override bool ResolveContract(Type type, out JsonContract contract)
        {
            var foundInCache = true;
            contract = contracts.GetOrAdd(type, key =>
            {
                foundInCache = false;
                return resolver.ResolveContract(type);
            });
            
            return foundInCache;
        }
    }
}