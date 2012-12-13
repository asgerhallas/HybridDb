using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace HybridDb
{
    public interface ISerializer
    {
        byte[] Serialize(object obj);
        T Deserialize<T>(byte[] data);
    }

    class Serializer : ISerializer 
    {
        readonly JsonSerializer serializer;

        public Serializer()
        {
            serializer = new JsonSerializer();
        }

        public byte[] Serialize(object obj)
        {
            using (var outStream = new MemoryStream())
            using (var bsonWriter = new BsonWriter(outStream))
            {
                serializer.Serialize(bsonWriter, obj);
                return outStream.ToArray();
            }
        }

        public T Deserialize<T>(byte[] data)
        {
            using (var inStream = new MemoryStream(data))
            using (var bsonReader = new BsonReader(inStream))
            {
                return serializer.Deserialize<T>(bsonReader);
            }
        }
    }
}