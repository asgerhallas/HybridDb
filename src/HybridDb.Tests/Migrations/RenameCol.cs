using System.Linq;
using HybridDb.Migration;
using Newtonsoft.Json.Linq;

namespace HybridDb.Tests.Migrations
{
    public class RenameCol : Migration.Migration
    {
        public RenameCol()
        {
            MigrateSchema()
                .ToVersion(1)
                .Migrate(Migration)
                .RewriteQueryUntilMigrated(RewriteUntilMigrated);
        }

        string RewriteUntilMigrated(string s)
        {
            return s.Replace("FROM Cases", "FROM Casez");
        }

        void Migration(ISchemaMigrator iMigrator)
        {
            
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