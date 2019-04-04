using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization
{
    public abstract class ContractMutator<T> : IContractMutator where T : JsonContract
    {
        public abstract void Mutate(T contract);

        public void Mutate(JsonContract contract)
        {
            var tContract = contract as T;
            if (tContract != null)
            {
                Mutate(tContract);
            }
        }
    }
}