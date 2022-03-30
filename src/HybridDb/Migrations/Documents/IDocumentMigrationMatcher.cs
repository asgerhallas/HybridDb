using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public interface IDocumentMigrationMatcher
    {
        public abstract SqlBuilder Matches(int? version);
        public abstract bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row);
    }
}