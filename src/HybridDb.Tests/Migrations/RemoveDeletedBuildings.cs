using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HybridDb.Tests.Migrations
{
    public class RemoveDeletedBuildings : Migration.Migration
    {
        public RemoveDeletedBuildings()
        {
            MigrateDocument()
                .FromTable("Cases")
                .ToVersion(1)
                .RequireSchemaVersion(1)
                .UseSerializer(new DefaultBsonSerializer())
                .MigrateOnRead<JObject>(MigrateOnRead);
        }

        public void MigrateOnRead(JObject document, IDictionary<string, object> projections)
        {
            foreach (var building in document["buildings"].Where(x => x.Value<bool>("IsDeleted")))
            {
                building.Remove();
            }
        }
    }
}