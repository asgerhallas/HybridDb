using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Documents
{
    public class UpdateProjectionsMigration : RowMigrationCommand
    {
        public override bool Matches(Configuration configuration, Table table) => table is DocumentTable;
        public override Sql Matches(IDocumentStore store, int? version) => Sql.From($"AwaitsReprojection = {true}");
        public override bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row) => true;

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row) => row;
    }
}