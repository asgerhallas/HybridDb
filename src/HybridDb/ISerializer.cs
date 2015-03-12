using System;
using Newtonsoft.Json.Linq;

namespace HybridDb
{
    public interface ISerializer
    {
        byte[] Serialize(object obj);
        object Deserialize(byte[] data, Type type);
        object Deserialize(JObject data, Type type);
    }
}