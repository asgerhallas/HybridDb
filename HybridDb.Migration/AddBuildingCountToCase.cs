using System.Collections.Generic;
using System.Linq;
using HybridDb.Schema;
using Newtonsoft.Json.Linq;

namespace HybridDb.Migration
{
    public class AddBuildingCountToCase : Migration
    {
        public override void Build()
        {
            MigrateSchema()
                .ToVersion(1)
                .Migrate(SchemaMigration);

            MigrateDocument()
                .FromTable("Cases")
                .ToVersion(1)
                .RequireSchemaVersion(1)
                .UseSerializer(new DefaultBsonSerializer())
                .MigrateOnWrite<JObject>(MigrateOnWrite);
        }

        void SchemaMigration(IMigrator migrator)
        {
            migrator.AddColumn("Cases", new UserColumn("BuildingsCount", typeof (int)));
        }

        public void MigrateOnWrite(JObject document, IDictionary<string, object> projections)
        {
            projections["BuildingsCount"] = document["Buildings"].Count();
        }
    }
}
