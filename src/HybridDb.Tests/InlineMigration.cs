using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Documents;
using HybridDb.Migrations.Schema;

namespace HybridDb.Tests
{
    public class InlineMigration : Migration
    {
        readonly List<RowMigrationCommand> documentCommands;
        readonly List<DdlCommand> schemaCommands;

        public InlineMigration(int version) : base(version)
        {
            documentCommands = new List<RowMigrationCommand>();
            schemaCommands = new List<DdlCommand>();
        }

        public InlineMigration(int version, params DdlCommand[] commands) : this(version) => schemaCommands = commands.ToList();

        public InlineMigration(int version, params RowMigrationCommand[] commands) : this(version) => documentCommands = commands.ToList();

        public override IEnumerable<DdlCommand> MigrateSchema(Configuration configuration) => schemaCommands;

        public override IEnumerable<RowMigrationCommand> MigrateDocument() => documentCommands;
    }
}