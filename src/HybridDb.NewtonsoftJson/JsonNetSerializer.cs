using System;
using HybridDb.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HybridDb.NewtonsoftJson
{
    public class JsonNetSerializer : DefaultSerializer
    {
        public new void AddConverter(JsonConverter converter)
        {
            base.AddConverter(converter);
        }

        public new void Order(int index, Func<JsonProperty, bool> predicate)
        {
            base.Order(index, predicate);
        }

        public new void Setup(Action<JsonSerializerSettings> action)
        {
            base.Setup(action);
        }

        public new void SetContractResolver(IExtendedContractResolver resolver)
        {
            base.SetContractResolver(resolver);
        }
    }
}