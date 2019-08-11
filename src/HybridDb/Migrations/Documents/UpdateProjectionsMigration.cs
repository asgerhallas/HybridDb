using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class UpdateProjectionsMigration : RowMigrationCommand<DocumentTable>
    {
        public UpdateProjectionsMigration() : base(null, null) { }

        public override SqlBuilder Matches(int? version) => new SqlBuilder().Append("AwaitsReprojection = @AwaitsReprojection", new Parameter("AwaitsReprojection", true));

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row) => row;
    }
}