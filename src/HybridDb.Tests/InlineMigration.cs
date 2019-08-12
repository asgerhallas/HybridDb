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

        public InlineMigration(int version, IEnumerable<DdlCommand> upfront, IEnumerable<RowMigrationCommand> background) : base(version)
        {
            schemaCommands = upfront.ToList();
            documentCommands = background.ToList();
        }

        public InlineMigration(int version) : this(version, new List<DdlCommand>(), new List<RowMigrationCommand>()) { }
        public InlineMigration(int version, params DdlCommand[] commands) : this(version, commands.ToList(), new List<RowMigrationCommand>()) { }
        public InlineMigration(int version, params RowMigrationCommand[] commands) : this(version, new List<DdlCommand>(), commands.ToList()) { }

        public override IEnumerable<DdlCommand> Upfront(Configuration configuration) => schemaCommands;
        public override IEnumerable<RowMigrationCommand> Background(Configuration configuration) => documentCommands;
    }
}