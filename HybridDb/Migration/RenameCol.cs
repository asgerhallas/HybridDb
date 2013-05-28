using System.Linq;
using Newtonsoft.Json.Linq;

namespace HybridDb.Migration
{
    public class RenameCol : Migration
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

        void Migration(IMigrator iMigrator)
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