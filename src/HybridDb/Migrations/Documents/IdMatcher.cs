using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Documents
{
    public class IdMatcher : IDocumentMigrationMatcher
    {
        public IdMatcher(IReadOnlyList<string> ids)
        {
            Ids = ids;
        }

        public IReadOnlyList<string> Ids { get; }

        public Sql Matches(IDocumentStore store, int? version) => Sql.From(Ids.Any(), $" and {DocumentTable.IdColumn} in {Ids}");

        public bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row)
        {
            var rowId = row.Get(DocumentTable.IdColumn);

            if (Ids.Any() && !Ids.Contains(rowId, StringComparer.InvariantCultureIgnoreCase)) return false;

            return true;
        }
    }
}