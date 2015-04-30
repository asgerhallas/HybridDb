using System.Collections.Generic;
using System.Linq;
using HybridDb.Migrations;

namespace HybridDb.Tests
{
    public class InlineMigration : Migration
    {
        readonly List<DocumentMigrationCommand> documentCommands;
        readonly List<SchemaMigrationCommand> schemaCommands;

        public InlineMigration(int version) : base(version)
        {
            documentCommands = new List<DocumentMigrationCommand>();
            schemaCommands = new List<SchemaMigrationCommand>();
        }

        public InlineMigration(int version, params SchemaMigrationCommand[] commands) : this(version)
        {
            schemaCommands = commands.ToList();
        }

        public InlineMigration(int version, params DocumentMigrationCommand[] commands) : this(version)
        {
            documentCommands = commands.ToList();
        }

        public override IEnumerable<SchemaMigrationCommand> MigrateSchema()
        {
            return schemaCommands;
        }

        public override IEnumerable<DocumentMigrationCommand> MigrateDocument()
        {
            return documentCommands;
        }
    }
}