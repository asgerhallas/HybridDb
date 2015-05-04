using System;
using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization
{
    public interface IExtendedContractResolver : IContractResolver
    {
        bool ResolveContract(Type type, out JsonContract contract);
    }
}