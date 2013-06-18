using System;
using System.Collections.Generic;
using HybridDb.Schema;

namespace HybridDb.Migration
{
    public class Migration : IHybridDbExtension
    {
        public List<DocumentMigrationDefinition> DocumentMigrations { get; private set; }
        public SchemaMigrationDefinition SchemaMigration { get; private set; }

        public Migration()
        {
            DocumentMigrations = new List<DocumentMigrationDefinition>();
        }

        public ISchemaMigrationBuilderStep1 MigrateSchema()
        {
            SchemaMigration = new SchemaMigrationDefinition();
            return SchemaMigration;
        }

        public IDocumentMigrationBuilderStep1 MigrateDocument()
        {
            var documentMigration = new DocumentMigrationDefinition();
            DocumentMigrations.Add(documentMigration);
            return documentMigration;
        }

        public class SchemaMigrationDefinition : ISchemaMigrationBuilderStep1,
                                                 ISchemaMigrationBuilderStep2
        {
            public int Version { get; private set; }
            public Action<ISchemaMigrator> Migration { get; private set; }
            public Func<string, string> RewriteQueries { get; private set; }

            ISchemaMigrationBuilderStep2 ISchemaMigrationBuilderStep1.ToVersion(int version)
            {
                Version = version;
                return this;
            }

            ISchemaMigrationBuilderStep2 ISchemaMigrationBuilderStep2.Migrate(Action<ISchemaMigrator> migration)
            {
                Migration = migration;
                return this;
            }

            ISchemaMigrationBuilderStep2 ISchemaMigrationBuilderStep2.RewriteQueryUntilMigrated(Func<string, string> rewrite)
            {
                RewriteQueries = rewrite;
                return this;
            }
        }

        public interface ISchemaMigrationBuilderStep1
        {
            ISchemaMigrationBuilderStep2 ToVersion(int version);
        }

        public interface ISchemaMigrationBuilderStep2
        {
            ISchemaMigrationBuilderStep2 Migrate(Action<ISchemaMigrator> migration);
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
            public Action<object, IDictionary<string, object>> MigrationOnRead { get; private set; }

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

            IDocumentMigrationBuilderStep5 IDocumentMigrationBuilderStep5.MigrateOnRead<T>(Action<T, IDictionary<string, object>> migration)
            {
                Type = typeof (T);
                MigrationOnRead = (doc, projections) => migration((T) doc, projections);
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
            IDocumentMigrationBuilderStep5 MigrateOnRead<T>(Action<T, IDictionary<string, object>> migration);
        }

        void IHybridDbExtension.OnRead(Table table, IDictionary<string, object> projections)
        {
            var documentTable = table as DocumentTable;
            if (documentTable != null)
            {
                new DocumentMigrator().OnRead(this, documentTable, projections);
            }
        }
    }
}