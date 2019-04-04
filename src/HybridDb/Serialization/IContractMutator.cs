using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization
{
    public interface IContractMutator
    {
        void Mutate(JsonContract contract);
    }
}