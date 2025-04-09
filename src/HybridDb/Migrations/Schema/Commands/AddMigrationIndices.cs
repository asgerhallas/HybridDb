using System.Linq;
using HybridDb.Config;
using HybridDb.Events;
using HybridDb.Queue;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Schema.Commands
{
    public class AddMigrationIndices : DdlCommand
    {
        public override void Execute(DocumentStore store)
        {
            foreach (var (name, table) in store.Configuration.Tables.OrderBy(x => x.Key))
            {
                if (table is not DocumentTable) continue;

                var formattedTableName = store.Database.FormatTableName(name);

                store.Database.RawExecute(Sql.From($"CREATE NONCLUSTERED INDEX [idx_Version] ON [{formattedTableName}] ( [{DocumentTable.VersionColumn.Name}] ASC)"), schema: true, commandTimeout: 300);
                store.Database.RawExecute(Sql.From($"CREATE NONCLUSTERED INDEX [idx_AwaitsReprojection] ON [{formattedTableName}] ( [{DocumentTable.AwaitsReprojectionColumn.Name}] ASC)"), schema: true, commandTimeout: 300 );
            }
        }

        public override string ToString() => "Add migration indices for Version and AwaitsReprojection.";
    }
}