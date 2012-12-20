using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace HybridDb
{
    public class DefaultJsonSerializer : DefaultBsonSerializer
    {
        public override byte[] Serialize(object obj)
        {
            using (var textWriter = new StringWriter())
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                CreateSerializer().Serialize(jsonWriter, obj);
                return Encoding.UTF8.GetBytes(textWriter.ToString());
            }
        }

        public override T Deserialize<T>(byte[] data)
        {
            using (var textReader = new StringReader(Encoding.UTF8.GetString(data)))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return CreateSerializer().Deserialize<T>(jsonReader);
            }
        }
    }
}