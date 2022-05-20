using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using HybridDb.Config;
using static Indentional.Text;

namespace HybridDb.Migrations.Documents
{
    public abstract class DocumentRowMigrationCommand : RowMigrationCommand
    {
        protected DocumentRowMigrationCommand(Type type, params IDocumentMigrationMatcher[] matchers)
        {
            Type = type;
            Matchers = matchers ?? throw new ArgumentNullException(nameof(matchers));
        }

        public Type Type { get; }
        public IDocumentMigrationMatcher[] Matchers { get; }

        public override bool Matches(Configuration configuration, Table table) =>
            table is DocumentTable && (
                Type == null || configuration.TryGetDesignFor(Type)?.Table == table
            );

        public override SqlBuilder Matches(IDocumentStore store, int? version)
        {
            var builder = new SqlBuilder()
                .Append(version != null, "Version < @version", new SqlParameter("version", version));

            return Matchers.Aggregate(builder, (current, matcher) => current.Append(matcher.Matches(store, version)));
        }

        public override bool Matches(int version, Configuration configuration, DocumentDesign design, IDictionary<string, object> row)
        {
            if (row.Get(DocumentTable.DiscriminatorColumn) != design.Discriminator)
            {
                throw new ArgumentException(Indent(@$"
                    Provided design must be the concrete design for the row.
                    The given design has discriminator {design.Discriminator}, 
                    but the row has discriminator {row.Get(DocumentTable.DiscriminatorColumn)}."));
            }

            if (row.Get(DocumentTable.VersionColumn) >= version) return false;

            foreach (var matcher in Matchers)
            {
                if (!matcher.Matches(version, configuration, design, row)) return false;
            }

            return Type == null || Type.IsAssignableFrom(design.DocumentType);
        }
    }
}