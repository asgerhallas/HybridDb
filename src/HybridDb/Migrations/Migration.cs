using System.Collections.Generic;
using HybridDb.Config;
using HybridDb.Migrations.Documents;
using HybridDb.Migrations.Schema;

namespace HybridDb.Migrations
{
    public abstract class Migration
    {
        protected Migration(int version) => Version = version;

        public int Version { get; }

        public virtual IEnumerable<DdlCommand> Upfront(Configuration configuration)
        {
            yield break;
        }

        public virtual IEnumerable<RowMigrationCommand> Background(Configuration configuration)
        {
            yield break;
        }
    }
}