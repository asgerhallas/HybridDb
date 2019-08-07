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
    /// </summary>
    public class HybridDb_1_x_x_to_2_x_x_Part1 : Migration
    {
        public HybridDb_1_x_x_to_2_x_x_Part1(int version) : base(version)
        {

        }

        public override IEnumerable<DdlCommand> MigrateSchema(Configuration configuration)
        {
            foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
            {
                yield return new RenameColumn(table, "Document", "Document_pre_v2");
                yield return new AddColumn(table.Name, new Column("Document", typeof(string), length: -1));

                yield return new RenameColumn(table, "Metadata", "Metadata_pre_v2");
                yield return new AddColumn(table.Name, new Column("Metadata", typeof(string), length: -1));
            }

            // Delete old document column in another migration for safety
        }

        public override IEnumerable<RowMigrationCommand> MigrateDocument()
        {
            yield return new MigrationCommand();
        }

        public class MigrationCommand : RowMigrationCommand
        {
            public MigrationCommand() : base(null, null) { }

            public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row)
            {
                row["Document"] = Encoding.UTF8.GetString((byte[])row["Document_pre_v2"]);
                row["Metadata"] = Encoding.UTF8.GetString((byte[])row["Metadata_pre_v2"]);
                return row;
            }
        }
    }

}