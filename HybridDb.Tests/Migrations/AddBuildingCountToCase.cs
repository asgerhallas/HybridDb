using System.Linq;
using HybridDb.Schema;
using Newtonsoft.Json.Linq;

namespace HybridDb.Tests.Migrations
{
    public class AddBuildingCountToCase : Migration.Migration
    {
        public AddBuildingCountToCase()
        {
            MigrateSchema()
                .ToVersion(1)
                .Migrate(migrator => migrator.AddColumn("Cases", new UserColumn("BuildingsCount", typeof (int))));

            MigrateDocument()
                .FromTable("Cases")
                .ToVersion(1)
                .RequireSchemaVersion(1)
                .UseSerializer(new DefaultBsonSerializer())
                .MigrateOnWrite<JObject>((document, projections) => projections["BuildingsCount"] = document["Buildings"].Count());
        }
    }
}
