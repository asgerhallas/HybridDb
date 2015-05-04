using System;
using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization
{
    public class AutomaticBackReferencesContractResolverDecorator : ExtendedContractResolver
    {
        readonly IExtendedContractResolver resolver;

        public AutomaticBackReferencesContractResolverDecorator(IExtendedContractResolver resolver)
        {
            this.resolver = resolver;
        }

        public override bool ResolveContract(Type type, out JsonContract contract)
        {
            if (resolver.ResolveContract(type, out contract)) 
                return true;

            Setup(contract as JsonObjectContract);
                
            return false;
        }

        static void Setup(JsonObjectContract contract)
        {
            if (contract == null)
                return;

            contract.OnSerializingCallbacks.Add((current, context) =>
                ((SerializationContext) context.Context).Push(current));

            contract.OnSerializedCallbacks.Add((current, context) =>
                ((SerializationContext) context.Context).Pop());

            contract.OnDeserializingCallbacks.Add((current, context) =>
                ((SerializationContext) context.Context).Push(current));

            contract.OnDeserializedCallbacks.Add((current, context) =>
                ((SerializationContext) context.Context).Pop());

            contract.OnSerializingCallbacks.Add((value, context) =>
                ((SerializationContext)context.Context).EnsureNoDuplicates(value));

            foreach (var property in contract.Properties)
            {
                //if (property.PropertyName == "Root")
                //    property.Ignored = true;

                // Assign a "once only" converter to handle back references.
                // This does not handle the DomainObject.AggregateRoot which is ignored below.
                if (typeof (object).IsAssignableFrom(property.PropertyType))
                {
                    property.Converter = property.MemberConverter = new BackReferenceConverter();
                }
            }
        }
    }
}