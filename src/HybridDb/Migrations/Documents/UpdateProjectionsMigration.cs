using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class UpdateProjectionsMigration : RowMigrationCommand
    {
        public override bool Matches(Configuration configuration, Table table) => table is DocumentTable;
        public override SqlBuilder Matches(int? version) => new SqlBuilder().Append("AwaitsReprojection = @AwaitsReprojection", new Parameter("AwaitsReprojection", true));
        public override bool Matches(int version, Configuration configuration, IDictionary<string, object> row) => true;

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row) => row;
    }
}