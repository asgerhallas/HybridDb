using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Documents
{
    public class IdPrefixMatcher(string idPrefix) : IDocumentMigrationMatcher
    {
        public string IdPrefix { get; } = idPrefix;

        public Sql Matches(IDocumentStore store, int? version) => Sql.From(!string.IsNullOrEmpty(IdPrefix), $" and Id LIKE {IdPrefix} + '%'");

        public bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row)
        {
            var rowId = row.Get(DocumentTable.IdColumn);

            if (!string.IsNullOrEmpty(IdPrefix) && !rowId.StartsWith(IdPrefix, StringComparison.InvariantCultureIgnoreCase)) return false;

            return true;
        }
    }
}