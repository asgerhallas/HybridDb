using System.Collections.Generic;
using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Documents
{
    public interface IDocumentMigrationMatcher
    {
        public abstract Sql Matches(IDocumentStore store, int? version);
        public abstract bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row);
    }
}