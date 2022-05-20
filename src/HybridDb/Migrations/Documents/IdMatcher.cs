using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class IdMatcher : IDocumentMigrationMatcher
    {
        public IdMatcher(IReadOnlyList<string> ids)
        {
            Ids = ids; // TODO: sanitize, as we don't use it as sql parameters
        }

        public IReadOnlyList<string> Ids { get; }

        public SqlBuilder Matches(IDocumentStore store, int? version) => new SqlBuilder()
            .Append(Ids.Any(), $" and Id in ({string.Join(", ", Ids.Select(x => $"'{x}'"))})");

        public bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row)
        {
            var rowId = row.Get(DocumentTable.IdColumn);

            if (Ids.Any() && !Ids.Contains(rowId, StringComparer.InvariantCultureIgnoreCase)) return false;

            return true;
        }
    }
}