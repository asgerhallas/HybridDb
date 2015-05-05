using System;
using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization.JsonNet
{
    public interface IExtendedContractResolver : IContractResolver
    {
        bool ResolveContract(Type type, out JsonContract contract);
    }
}