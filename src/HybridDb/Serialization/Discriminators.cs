using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Serialization
{
    public class Discriminators
    {
        readonly Dictionary<string, Type> discriminatorToType;
        readonly Dictionary<Type, string> typeToDiscriminator;

        public Discriminators(params Discriminator[] discriminators)
        {
            discriminatorToType = discriminators.ToDictionary(x => x.Name, x => x.Type);
            typeToDiscriminator = discriminators.ToDictionary(x => x.Type, x => x.Name);
        }

        public bool TryGetFromType(Type type, out string discriminator)
        {
            return typeToDiscriminator.TryGetValue(type, out discriminator);
        }

        public string GetFromTypeOrDefault(Type type)
        {
            string discriminator;
            return TryGetFromType(type, out discriminator) ? discriminator : null;
        }

        public bool TryGetFromDiscriminator(string discriminator, out Type type)
        {
            return discriminatorToType.TryGetValue(discriminator, out type);
        }

        public Type GetFromDiscriminatorOrDefault(string discriminator)
        {
            Type type;
            return TryGetFromDiscriminator(discriminator, out type) ? type : null;
        }

        public bool IsDiscriminated(Type objectType)
        {
            if (!Discriminator.IsDiscriminatable(objectType))
                return false;

            return typeToDiscriminator.Any(pair => objectType.IsAssignableFrom(pair.Key));
        }
    }
}