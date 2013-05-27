using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ClassLibrary1
{
    public class Class1
    {
        [Fact]
        public void FactMethodName()
        {
            var jObject = JObject.Parse(File.ReadAllText("large.json"));

            byte[] bytes = null;
            using (var outStream = new MemoryStream())
            using (var bsonWriter = new BsonWriter(outStream))
            {
                new JsonSerializer().Serialize(bsonWriter, jObject);
                bytes = outStream.ToArray();
            }

            const int a = 5;

            var w = Stopwatch.StartNew();
            var serializer = new JsonSerializer();
            for (int i = 0; i < a; i++)
            {
                using (var inStream = new MemoryStream(bytes))
                using (var bsonReader = new BsonReader(inStream))
                {
                    serializer.Deserialize(bsonReader, typeof(Dictionary<string, object>));
                }
            }

            Console.WriteLine(w.ElapsedMilliseconds / a);
            w.Restart();

            var jsonSerializer = new JsonSerializer();
            for (int i = 0; i < a; i++)
            {
                using (var inStream = new MemoryStream(bytes))
                using (var bsonReader = new BsonReader(inStream))
                {
                    var deserialize = jsonSerializer.Deserialize<JObject>(bsonReader);
                    using (var reader = new JTokenReader(deserialize))
                    {
                        jsonSerializer.Deserialize(reader, typeof(Dictionary<string, object>));
                    }
                }
            }

            Console.WriteLine(w.ElapsedMilliseconds / a);
        }
    }
}
