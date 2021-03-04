using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HybridDb.Config;
using static Indentional.Indent;

namespace HybridDb.Migrations.Documents
{
    public abstract class DocumentRowMigrationCommand : RowMigrationCommand
    {
        protected DocumentRowMigrationCommand(Type type, string idPrefix)
        {
            Type = type;
            IdPrefix = idPrefix;
        }

        public Type Type { get; }
        public string IdPrefix { get; }

        public override bool Matches(Configuration configuration, Table table) =>
            table is DocumentTable && (
                Type == null || configuration.TryGetDesignFor(Type)?.Table == table
            );

        public override SqlBuilder Matches(int? version) => new SqlBuilder()
            .Append(version != null, "Version < @version", new SqlParameter("version", version))
            .Append(!string.IsNullOrEmpty(IdPrefix), " and Id LIKE @idPrefix + '%'", new SqlParameter("idPrefix", IdPrefix));

        public override bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row)
        {
            var rowId = row.Get(DocumentTable.IdColumn);

            if (row.Get(DocumentTable.DiscriminatorColumn) != design.Discriminator)
            {
                throw new ArgumentException(_(@$"
                    Provided design must be the concrete design for the row.
                    The given design has discriminator {design.Discriminator}, 
                    but the row has discriminator {row.Get(DocumentTable.DiscriminatorColumn)}."));
            }

            return row.Get(DocumentTable.VersionColumn) < version &&
                   (string.IsNullOrEmpty(IdPrefix) || rowId.StartsWith(IdPrefix)) &&
                   (Type == null || Type.IsAssignableFrom(design.DocumentType));
        }
    }
}