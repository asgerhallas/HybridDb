using System;
using System.Linq;
using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization
{
    public class DiscriminatorContractResolverDecorator : ExtendedContractResolver
    {
        readonly IExtendedContractResolver resolver;
        readonly Discriminators discriminators;

        public DiscriminatorContractResolverDecorator(IExtendedContractResolver resolver, Discriminators discriminators)
        {
            this.resolver = resolver;
            this.discriminators = discriminators;
        }

        public override bool ResolveContract(Type type, out JsonContract contract)
        {
            if (resolver.ResolveContract(type, out contract))
                return true;

            Setup(contract as JsonObjectContract);

            return false;
        }

        void Setup(JsonObjectContract contract)
        {
            if (contract == null) return;

            if (!discriminators.IsDiscriminated(contract.CreatedType))
                return;

            contract.Properties.Insert(0, new JsonProperty
            {
                PropertyName = "Discriminator",
                PropertyType = typeof(string),
                ValueProvider = new DiscriminatorValueProvider(discriminators),
                Writable = false,
                Readable = true
            });
        }
    }
}