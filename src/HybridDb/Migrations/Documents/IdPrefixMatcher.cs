using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class IdPrefixMatcher : IDocumentMigrationMatcher
    {
        public IdPrefixMatcher(string idPrefix) => IdPrefix = idPrefix;
        
        public string IdPrefix { get; }

        public SqlBuilder Matches(IDocumentStore store, int? version) => new SqlBuilder()
            .Append(!string.IsNullOrEmpty(IdPrefix), " and Id LIKE @idPrefix + '%'", new SqlParameter("idPrefix", IdPrefix));

        public bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row)
        {
            var rowId = row.Get(DocumentTable.IdColumn);

            if (!string.IsNullOrEmpty(IdPrefix) && !rowId.StartsWith(IdPrefix, StringComparison.InvariantCultureIgnoreCase)) return false;

            return true;
        }
    }
}