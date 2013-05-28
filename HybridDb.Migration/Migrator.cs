using System;
using System.Collections.Generic;
using HybridDb.Schema;
using Newtonsoft.Json.Linq;

namespace HybridDb.Migration
{
    public class Migrator
    {
        public void OnRead(Migration migration, IDictionary<string, object> projections)
        {
            var table = new Table(migration.DocumentMigration.Tablename);
            var id = (Guid)projections[table.IdColumn.Name];
            var version = (int)projections[table.VersionColumn.Name];
            var expectedVersion = migration.DocumentMigration.Version - 1;

            if (version != expectedVersion)
            {
                throw new ArgumentException(string.Format("Row with id {0} is version {1}. " +
                                                          "This document migration requires the current version to be {2}. " +
                                                          "Please migrate all documents to version {2} and retry.",
                                                          id, version, expectedVersion));
            }

            var serializer = migration.DocumentMigration.Serializer;
            var document = serializer.Deserialize((byte[]) projections[table.DocumentColumn.Name], typeof (JObject));
            migration.DocumentMigration.MigrationOnRead(document);
            projections[table.VersionColumn.Name] = version + 1;
            projections[table.DocumentColumn.Name] = serializer.Serialize(document);
        }
    }
}