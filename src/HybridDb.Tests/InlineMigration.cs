using System.Collections.Generic;
using System.Linq;
using HybridDb.Migrations;
using HybridDb.Migrations.Documents;
using HybridDb.Migrations.Schema;

namespace HybridDb.Tests
{
    public class InlineMigration : Migration
    {
        readonly List<DocumentMigrationCommand> documentCommands;
        readonly List<DdlCommand> schemaCommands;

        public InlineMigration(int version) : base(version)
        {
            documentCommands = new List<DocumentMigrationCommand>();
            schemaCommands = new List<DdlCommand>();
        }

        public InlineMigration(int version, params DdlCommand[] commands) : this(version)
        {
            schemaCommands = commands.ToList();
        }

        public InlineMigration(int version, params DocumentMigrationCommand[] commands) : this(version)
        {
            documentCommands = commands.ToList();
        }

        public override IEnumerable<DdlCommand> MigrateSchema()
        {
            return schemaCommands;
        }

        public override IEnumerable<DocumentMigrationCommand> MigrateDocument()
        {
            return documentCommands;
        }
    }
}