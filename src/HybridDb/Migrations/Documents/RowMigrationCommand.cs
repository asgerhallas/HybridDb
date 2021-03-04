using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public abstract class RowMigrationCommand
    {
        public abstract bool Matches(Configuration configuration, Table table);
        public abstract SqlBuilder Matches(int? version);
        public abstract bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row);
        public abstract IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row);
    }
}