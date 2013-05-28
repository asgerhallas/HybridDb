using System.Linq;
using Newtonsoft.Json.Linq;

namespace HybridDb.Migration
{
    public class RemoveDeletedBuildings : Migration
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

        public void MigrateOnRead(JObject document)
        {
            foreach (var building in document["buildings"].Where(x => x.Value<bool>("IsDeleted")))
            {
                building.Remove();
            }
        }
    }
}