using System;
using System.Collections.Generic;
using HybridDb.Schema;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HybridDb.Migration
{
    public class MigrationAddIn : DocumentStoreAddIn
    {
        readonly Migration migration;

        public MigrationAddIn(Migration migration)
        {
            this.migration = migration;
        }

        public void OnRead(Dictionary<string, object> projections)
        {
            new Migrator().OnRead(migration, projections);
        }
    }

    public class Migrator
    {
        public void OnRead(Migration migration, Dictionary<string, object> projections)
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

            var document = migration.DocumentMigration.Serializer.Deserialize((byte[]) projections[table.DocumentColumn.Name], typeof (JObject));
            migration.DocumentMigration.MigrationOnRead(document);
            projections[table.VersionColumn.Name] = version + 1;
            projections[table.DocumentColumn.Name] = migration.DocumentMigration.Serializer.Serialize(document);
        }
    }

    public class Runner
    {
        readonly IDocumentStore store;

        public Runner(IDocumentStore store)
        {
            this.store = store;
        }

        public void Run(Migration migration)
        {
            var migrator = new Migrator();

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
                    
                    var rows = store.Query<Dictionary<string, object>>(table, out stats, where: @where, take: 100).ToList();
                    if (rows.Count == 0)
                        break;

                    foreach (var row in rows)
                    {
                        migrator.OnRead(migration, row);

                        var id = (Guid)row[table.IdColumn.Name];
                        var etag = (Guid)row[table.EtagColumn.Name];
                        var document = (Guid)row[table.EtagColumn.Name];


                        try
                        {
                            store.Update(table, id, etag, (byte[])row[table.DocumentColumn], row);
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

    public abstract class Migration
    {
        public DocumentMigrationDefinition DocumentMigration { get; private set; }
        public SchemaMigrationDefinition SchemaMigration { get; private set; }

        public abstract void Build();

        public ISchemaMigrationBuilderStep1 MigrateSchema()
        {
            SchemaMigration = new SchemaMigrationDefinition();
            return SchemaMigration;
        }

        public IDocumentMigrationBuilderStep1 MigrateDocument()
        {
            DocumentMigration = new DocumentMigrationDefinition();
            return DocumentMigration;
        }

        public class SchemaMigrationDefinition : ISchemaMigrationBuilderStep1,
                                                 ISchemaMigrationBuilderStep2
        {
            public int Version { get; private set; }
            protected Action<IMigrator> Migration { get; private set; }
            protected Func<string, string> Rewrite { get; private set; }

            ISchemaMigrationBuilderStep2 ISchemaMigrationBuilderStep1.ToVersion(int version)
            {
                Version = version;
                return this;
            }

            ISchemaMigrationBuilderStep2 ISchemaMigrationBuilderStep2.Migrate(Action<IMigrator> migration)
            {
                Migration = migration;
                return this;
            }

            ISchemaMigrationBuilderStep2 ISchemaMigrationBuilderStep2.RewriteQueryUntilMigrated(Func<string, string> rewrite)
            {
                Rewrite = rewrite;
                return this;
            }
        }

        public interface ISchemaMigrationBuilderStep1
        {
            ISchemaMigrationBuilderStep2 ToVersion(int version);
        }

        public interface ISchemaMigrationBuilderStep2
        {
            ISchemaMigrationBuilderStep2 Migrate(Action<IMigrator> migration);
            ISchemaMigrationBuilderStep2 RewriteQueryUntilMigrated(Func<string, string> rewrite);
        }

        public class DocumentMigrationDefinition : IDocumentMigrationBuilderStep1,
                                                   IDocumentMigrationBuilderStep2,
                                                   IDocumentMigrationBuilderStep3,
                                                   IDocumentMigrationBuilderStep4,
                                                   IDocumentMigrationBuilderStep5
        {
            public int RequiredSchemaVersion { get; private set; }
            public int Version { get; private set; }
            public string Tablename { get; private set; }
            public ISerializer Serializer { get; private set; }
            public Type Type { get; private set; }
            public Action<object> MigrationOnRead { get; private set; }
            public Action<object, IDictionary<string, object>> MigrationOnWrite { get; private set; }

            IDocumentMigrationBuilderStep2 IDocumentMigrationBuilderStep1.FromTable(string tablename)
            {
                Tablename = tablename;
                return this;
            }

            IDocumentMigrationBuilderStep3 IDocumentMigrationBuilderStep2.ToVersion(int version)
            {
                Version = version;
                return this;
            }

            IDocumentMigrationBuilderStep4 IDocumentMigrationBuilderStep3.RequireSchemaVersion(int version)
            {
                RequiredSchemaVersion = version;
                return this;
            }

            IDocumentMigrationBuilderStep5 IDocumentMigrationBuilderStep4.UseSerializer(ISerializer serializer)
            {
                Serializer = serializer;
                return this;
            }

            IDocumentMigrationBuilderStep5 IDocumentMigrationBuilderStep5.MigrateOnRead<T>(Action<T> migration)
            {
                Type = typeof (T);
                MigrationOnRead = x => migration((T) x);
                return this;
            }

            IDocumentMigrationBuilderStep5 IDocumentMigrationBuilderStep5.MigrateOnWrite<T>(Action<T, IDictionary<string, object>> migration)
            {
                Type = typeof(T);
                MigrationOnWrite = (x, y) => migration((T) x, y);
                return this;
            }
        }

        public interface IDocumentMigrationBuilderStep1
        {
            IDocumentMigrationBuilderStep2 FromTable(string tablename);
        }

        public interface IDocumentMigrationBuilderStep2
        {
            IDocumentMigrationBuilderStep3 ToVersion(int version);
        }

        public interface IDocumentMigrationBuilderStep3
        {
            IDocumentMigrationBuilderStep4 RequireSchemaVersion(int version);
        }

        public interface IDocumentMigrationBuilderStep4
        {
            IDocumentMigrationBuilderStep5 UseSerializer(ISerializer serializer);
        }

        public interface IDocumentMigrationBuilderStep5
        {
            IDocumentMigrationBuilderStep5 MigrateOnRead<T>(Action<T> migration);
            IDocumentMigrationBuilderStep5 MigrateOnWrite<T>(Action<T, IDictionary<string, object>> migration);
        }
    }
}