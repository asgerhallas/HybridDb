using System;
using Newtonsoft.Json;

namespace HybridDb.Serialization
{
    public class BackReferenceConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var context = ((SerializationContext)serializer.Context.Context);

            if (context.HasRoot && (string)reader.Value == "root")
            {
                return context.Root;
            }

            if (context.HasParent && (string)reader.Value == "parent")
            {
                return context.Parent;
            }

            return serializer.Deserialize(reader, objectType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var context = ((SerializationContext)serializer.Context.Context);

            if (context.HasRoot && ReferenceEquals(value, context.Root))
            {
                writer.WriteValue("root");
                return;
            }

            if (context.HasParent && ReferenceEquals(value, context.Parent))
            {
                writer.WriteValue("parent");
                return;
            }

            serializer.Serialize(writer, value);
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotSupportedException(
                "This converter is only supposed to be used directly on JsonProperty.Converter and JsonProperty.MemberConverter. " +
                "If it is registered on the serializer to handle all types it will loop infinitely.");
        }
    }
}