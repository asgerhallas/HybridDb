using HybridDb.Config;
using HybridDb.Events;
using HybridDb.Queue;

namespace HybridDb.Migrations.Schema.Commands
{
    public class AddMigrationIndices : DdlCommand
    {
        public override void Execute(DocumentStore store)
        {
            foreach (var (name, table) in store.Configuration.Tables)
            {
                if (table is not DocumentTable) continue;

                var formattedTableName = store.Database.FormatTableName(name);

                store.Database.RawExecute($"CREATE NONCLUSTERED INDEX [idx_Version] ON [{formattedTableName}] ( [{DocumentTable.VersionColumn.Name}] ASC)");
                store.Database.RawExecute($"CREATE NONCLUSTERED INDEX [idx_AwaitsReprojection] ON [{formattedTableName}] ( [{DocumentTable.AwaitsReprojectionColumn.Name}] ASC)");
            }
        }

        public override string ToString() => "Add migration indices for Version and AwaitsReprojection.";
    }
}