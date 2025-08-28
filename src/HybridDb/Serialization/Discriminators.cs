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
            discriminatorToType = discriminators.ToDictionary(x => x.Name, x => x.Basetype);
            typeToDiscriminator = discriminators.ToDictionary(x => x.Basetype, x => x.Name);
        }

        public bool TryGetFromType(Type type, out string discriminator) =>
            typeToDiscriminator.TryGetValue(type, out discriminator);

        public string GetFromTypeOrDefault(Type type) =>
            TryGetFromType(type, out var discriminator) ? discriminator : null;

        public bool TryGetFromDiscriminator(string discriminator, out Type type) =>
            discriminatorToType.TryGetValue(discriminator, out type);

        public Type GetFromDiscriminatorOrDefault(string discriminator) =>
            TryGetFromDiscriminator(discriminator, out var type) ? type : null;

        public bool IsDiscriminated(Type objectType) =>
            Discriminator.IsDiscriminatable(objectType) &&
            typeToDiscriminator.Any(pair => objectType.IsAssignableFrom(pair.Key));
    }
}