using System.Collections.Generic;

namespace HybridDb.Migrations.Documents
{
    public class UpdateProjectionsMigration : RowMigrationCommand
    {
        public UpdateProjectionsMigration() : base(null, null) { }

        public override string Where => "AwaitsReprojection = @AwaitsReprojection";

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row) => row;
    }
}