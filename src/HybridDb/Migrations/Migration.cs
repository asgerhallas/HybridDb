using System.Collections.Generic;

namespace HybridDb.Migrations
{
    public abstract class Migration
    {
        protected Migration(int version)
        {
            Version = version;
        }

        public int Version { get; private set; }

        public virtual IEnumerable<SchemaMigrationCommand> MigrateSchema()
        {
            yield break;
        }

        public virtual IEnumerable<DocumentMigrationCommand> MigrateDocument()
        {
            yield break;
        }
    }
}