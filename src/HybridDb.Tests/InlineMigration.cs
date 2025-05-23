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
        readonly IEnumerable<DdlCommand> before;
        readonly IEnumerable<DdlCommand> after;
        readonly IEnumerable<RowMigrationCommand> documentCommands;

        public InlineMigration(int version, 
            IEnumerable<DdlCommand> before = null, 
            IEnumerable<DdlCommand> after = null, 
            IEnumerable<RowMigrationCommand> background = null
        ) : base(version)
        {
            this.before = before ?? new List<DdlCommand>();
            this.after = after ?? new List<DdlCommand>();

            documentCommands = background ?? new List<RowMigrationCommand>();
        }

        public InlineMigration(int version) 
            : this(version, new List<DdlCommand>(), new List<DdlCommand>(), new List<RowMigrationCommand>()) { }
        
        public InlineMigration(int version, params RowMigrationCommand[] commands) 
            : this(version, new List<DdlCommand>(), new List<DdlCommand>(), commands.ToList()) { }

        public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration) => before;
        public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration) => after;
        public override IEnumerable<RowMigrationCommand> Background(Configuration configuration) => documentCommands;
    }
}