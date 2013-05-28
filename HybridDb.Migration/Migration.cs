using System;
using System.Collections.Generic;

namespace HybridDb.Migration
{
    public abstract class Migration : IAddIn
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

        void IAddIn.OnRead(IDictionary<string, object> projections)
        {
            new Migrator().OnRead(this, projections);
        }
    }
}