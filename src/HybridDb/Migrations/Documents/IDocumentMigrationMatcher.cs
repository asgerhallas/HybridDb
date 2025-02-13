using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public interface IDocumentMigrationMatcher
    {
        public abstract SqlBuilderOld Matches(IDocumentStore store, int? version);
        public abstract bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row);
    }
}