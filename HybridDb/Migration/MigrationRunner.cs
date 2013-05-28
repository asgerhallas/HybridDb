using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Schema;

namespace HybridDb.Migration
{
    public class MigrationRunner
    {
        readonly IDocumentStore store;

        public MigrationRunner(IDocumentStore store)
        {
            this.store = store;
        }

        public void Run(Migration migration)
        {
            var migrator = new DocumentMigrator();

            var documentMigration = migration.DocumentMigration;
            if (documentMigration != null)
            {
                if (documentMigration.Tablename == null)
                    throw new ArgumentException("Document migration must have a tablename");

                if (documentMigration.Version == 0)
                    throw new ArgumentException("Document migration must have a version number larger than 0");

                var table = new Table(documentMigration.Tablename);
                while (true)
                {
                    QueryStats stats;
                    var @where = string.Format("Version < {0}", documentMigration.Version);
                    
                    var rows = store.Query<Dictionary<string, object>>(table, out stats, @where: @where, take: 100).ToList();
                    if (rows.Count == 0)
                        break;

                    foreach (var row in rows)
                    {
                        migrator.OnRead(migration, row);

                        var id = (Guid)row[table.IdColumn.Name];
                        var etag = (Guid)row[table.EtagColumn.Name];
                        var document = (byte[])row[table.DocumentColumn.Name];

                        try
                        {
                            store.Update(table, id, etag, document, row);
                        }
                        catch (ConcurrencyException)
                        {
                            // We don't care. Either the version is bumped by other user or we'll retry in next round.
                        }
                    }
                }
            }
        }
    }
}