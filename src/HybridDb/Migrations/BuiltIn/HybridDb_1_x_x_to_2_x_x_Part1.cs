using System.Collections.Generic;
using System.Linq;
using System.Text;
using HybridDb.Config;
using HybridDb.Migrations.Documents;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;

namespace HybridDb.Migrations.BuiltIn
{
    /// <summary>
    /// Conversion of Document and Metadata column from varbinary to nvarchar(max).
    /// This migration will not delete temporary columns with the old document format, you
    /// must manually issue _another_ migration in _another_ deploy to do the clean up.
    /// Before doing that make sure all document migrations are fully completed, as deleting these
    /// columns for documents that has not been migrated to the new format will result ind data loss!
    /// </summary>
    public class HybridDb_1_x_x_to_2_x_x_Part1 : Migration
    {
        public HybridDb_1_x_x_to_2_x_x_Part1(int version) : base(version) { 
        }

        public override IEnumerable<DdlCommand> Upfront(Configuration configuration)
        {
            foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
            {
                yield return new RenameColumn(table, "Document", "Document_pre_v2");
                yield return new AddColumn(table.Name, new Column("Document", typeof(string), length: -1));

                yield return new RenameColumn(table, "Metadata", "Metadata_pre_v2");
                yield return new AddColumn(table.Name, new Column("Metadata", typeof(string), length: -1));
            }
        }

        public override IEnumerable<RowMigrationCommand> Background(Configuration configuration)
        {
            yield return new MigrationCommand();
        }

        public class MigrationCommand : DocumentRowMigrationCommand
        {
            public MigrationCommand() : base(null, null) { }

            public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row)
            {
                var prevDocument = row.Get<byte[]>("Document_pre_v2");
                if (prevDocument != null)
                {
                    row.Set(DocumentTable.DocumentColumn, Encoding.UTF8.GetString(prevDocument));
                }

                var prevMetadata = row.Get<byte[]>("Metadata_pre_v2");
                if (prevMetadata != null)
                {
                    row.Set(DocumentTable.MetadataColumn, Encoding.UTF8.GetString(prevMetadata));
                }

                return row;
            }
        }
    }

}