using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;

namespace HybridDb.Migrations.BuiltIn
{
    public class RemoveLegacyDocumentTableChangeTracking : Migration
    {
        public RemoveLegacyDocumentTableChangeTracking(int version) : base(version) { }

        public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
        {
            yield return new RemoveLegacyDocumentTableChangeTrackingCommand();
        }

        class RemoveLegacyDocumentTableChangeTrackingCommand : DdlCommand
        {
            const byte DeletedOperation = 4;

            public override void Execute(DocumentStore store)
            {
                var schema = store.Database.QuerySchema();

                foreach (var table in store.Configuration.Tables.Values.OfType<DocumentTable>())
                {
                    if (!schema.TryGetValue(table.Name, out var columns))
                    {
                        continue;
                    }

                    var hasLastOperation = columns.Any(x => x.Equals("LastOperation"));
                    var hasTimestamp = columns.Any(x => x.Equals("Timestamp"));

                    if (!hasLastOperation && !hasTimestamp)
                    {
                        continue;
                    }

                    if (hasLastOperation)
                    {
                        store.Database.RawExecute(
                            $"delete from {store.Database.FormatTableNameAndEscape(table.Name)} where [LastOperation] = @DeletedOperation",
                            new { DeletedOperation },
                            commandTimeout: 300);

                        store.Execute(new RemoveColumn(table, "LastOperation"));
                    }

                    if (hasTimestamp)
                    {
                        store.Execute(new RemoveColumn(table, "Timestamp"));
                    }
                }
            }

            public override string ToString() => "Remove legacy live-query columns and deleted tombstones from document tables";
        }
    }
}
