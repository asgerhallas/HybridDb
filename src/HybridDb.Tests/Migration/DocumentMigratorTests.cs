using System;
using System.Collections.Generic;
using System.IO;
using HybridDb.Migration;
using HybridDb.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migration
{
    public class DocumentMigratorTests
    {
        [Fact(Skip = "feature tbd")]
        public void CanManipulateDocumentOnRead()
        {
            var serializer = new DefaultBsonSerializer();

            var migration = new HybridDb.Migration.Migration();
            migration.MigrateDocument()
                     .FromTable("Entities")
                     .ToVersion(2)
                     .RequireSchemaVersion(1)
                     .UseSerializer(serializer)
                     .MigrateOnRead<JObject>((doc, proj) => doc["Property"] = 10);

            var projections = new Dictionary<string, object>
            {
                {"Id", Guid.NewGuid()}, 
                {"Version", 1}, 
                {"Document", serializer.Serialize(JObject.Parse("{ Property: 1 }"))},
            };
            
            new DocumentMigrator().OnRead(migration, new DocumentTable("Entities"), projections);

            var document = (JObject)serializer.Deserialize((byte[]) projections["Document"], typeof(JObject));
            ((int)document["Property"]).ShouldBe(10);
            ((int)projections["Version"]).ShouldBe(2);
        }

        [Fact(Skip = "feature tbd")]
        public void FailsOnReadIfCurrentDocumentIsWrongVersion()
        {
            var migration = new HybridDb.Migration.Migration();
            migration.MigrateDocument()
                .FromTable("Entities")
                .ToVersion(3)
                .RequireSchemaVersion(1)
                .UseSerializer(null)
                .MigrateOnRead<object>((doc, proj) => { });

            var projections = new Dictionary<string, object>
            {
                {"Id", Guid.NewGuid()}, 
                {"Version", 1}, 
                {"Document", new byte[0]},
            };

            Should.Throw<ArgumentException>(() => new DocumentMigrator().OnRead(migration, new DocumentTable("Entities"), projections));
        }

        public class Entity
        {
            public Guid Id { get; set; }
            public int Property { get; set; }
        }
    }

    public static class JObjectEx
    {
        public static byte[] ToByteArray(this JObject self)
        {
            using (var sout = new MemoryStream())
            using (var bsonWriter = new BsonWriter(sout))
            {
                new JsonSerializer().Serialize(bsonWriter, self);
                return sout.ToArray();
            }
        }
    }
}