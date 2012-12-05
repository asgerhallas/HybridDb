using System;
using System.Data;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace HybridDb
{
    public class DocumentColumn : IColumnConfiguration
    {
        readonly JsonSerializer serializer;

        public DocumentColumn(JsonSerializer serializer)
        {
            this.serializer = serializer;
        }

        public string Name
        {
            get { return "Document"; }
        }

        public Column Column
        {
            get { return new Column(DbType.Binary, Int32.MaxValue); }
        }

        public object GetValue(object document)
        {
            using (var outStream = new MemoryStream())
            using (var bsonWriter = new BsonWriter(outStream))
            {
                serializer.Serialize(bsonWriter, document);
                return outStream.ToArray();
            }
        }
    }
}