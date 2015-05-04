using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HybridDb.Serialization
{
    public class DiscriminatedTypeConverter : JsonConverter
    {
        readonly Discriminators discriminators;
        readonly List<JsonConverter> converters;

        public DiscriminatedTypeConverter(Discriminators discriminators, List<JsonConverter> converters)
        {
            this.discriminators = discriminators;
            this.converters = converters;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jObject = JToken.Load(reader);

            if (jObject.Type == JTokenType.Null)
                return null;

            var discriminator = jObject.Value<string>("Discriminator");
            
            if (discriminator == null) return jObject;

            Type type;
            if (!discriminators.TryGetFromDiscriminator(discriminator, out type))
            {
                throw new InvalidOperationException(string.Format("Could not find a type from discriminator {0}", discriminator));
            }

            var contract = serializer.ContractResolver.ResolveContract(type);
            var target = contract.DefaultCreator();

            serializer.Populate(jObject.CreateReader(), target);

            return target;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) { }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override bool CanConvert(Type objectType)
        {
            return discriminators.IsDiscriminated(objectType);
        }
    }
}