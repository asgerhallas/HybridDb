using System;
using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization
{
    public abstract class ExtendedContractResolver : IExtendedContractResolver
    {
        public JsonContract ResolveContract(Type type)
        {
            JsonContract contract;
            ResolveContract(type, out contract);
            return contract;
        }

        /// <summary>
        /// Returns a boolean that indicates if the contract has already been initialized,
        /// so decorators can know if they have additional work to do or can just use the contract as is.
        /// </summary>
        public abstract bool ResolveContract(Type type, out JsonContract contract);
    }
}