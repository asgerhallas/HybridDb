using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migration
{
    public class DocumentMigrator
    {
        public void OnRead(Migration migration, DocumentTable table, IDictionary<string, object> projections)
        {
            foreach (var documentMigration in migration.DocumentMigrations.Where(x => x.Tablename == table.Name))
            {
                OnRead(documentMigration, table, projections);
            }
        }

        public void OnRead(Migration.DocumentMigrationDefinition documentMigration, DocumentTable table, IDictionary<string, object> projections)
        {
            if (documentMigration.MigrationOnRead == null)
                return;

            var currentVersion = AssertCorrectVersion(documentMigration, table, projections);

            var serializer = documentMigration.Serializer;
            var document = serializer.Deserialize((byte[]) projections[table.DocumentColumn.Name], documentMigration.Type);
            documentMigration.MigrationOnRead(document, projections);
            projections[table.VersionColumn.Name] = currentVersion + 1;
            projections[table.DocumentColumn.Name] = serializer.Serialize(document);
        }

        private static int AssertCorrectVersion(Migration.DocumentMigrationDefinition documentMigration, DocumentTable table, IDictionary<string, object> projections)
        {
            var id = (Guid) projections[table.IdColumn.Name];
            var version = (int) projections[table.VersionColumn.Name];
            var expectedVersion = documentMigration.Version - 1;

            if (version != expectedVersion)
            {
                throw new ArgumentException(string.Format("Row with id {0} is version {1}. " +
                                                          "This document migration requires the current version to be {2}. " +
                                                          "Please migrate all documents to version {2} and retry.",
                                                          id, version, expectedVersion));
            }

            return version;
        }
    }
}