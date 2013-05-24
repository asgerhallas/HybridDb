using System;
using System.Collections.Generic;
using HybridDb.Schema;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace HybridDb.Migration
{
    public class AddBuildingCountToCase : Migration
    {
        public override void Build()
        {
            MigrateSchema()
                .ToVersion(1)
                .Migrate(SchemaMigration);


            MigrateDocument()
                .FromTable("Cases")
                .ToVersion(1)
                .RequireSchemaVersion(1)
                .UseSerializer(new DefaultBsonSerializer())
                .MigrateOnWrite(MigrateOnWrite);
        }

        void SchemaMigration(IMigrator migrator)
        {
            migrator.AddColumn("Cases", new UserColumn("BuildingsCount", typeof (int)));
        }

        public void MigrateOnWrite(object document, IDictionary<string, object> projections)
        {
            projections["BuildingsCount"] = document["Buildings"].Count();
        }
    }

    public interface ISchemaMigration
    {
        int ToSchemaVersion { get; }
        void MigrateSchema(IMigrator migrator);
        string RewriteQuery(string query);
    }

    public interface IDocumentMigration<T>
    {
        string Tablename { get; }
        int RequiredSchemaVersion { get; }
        int ToDocumentVersion { get; }
        ISerializer Serializer { get; }
        void MigrateOnRead(T document, IDictionary<string, object> projections);
        void MigrateOnWrite(T document, IDictionary<string, object> projections);
    }
    
    public abstract class Migration
    {
        SchemaMigrationDefinition schemaMigrationDefinition;
        DocumentMigrationDefinition documentMigrationDefinition;

        public abstract void Build();

        public SchemaMigrationDefinition.ISchemaMigrationBuilderStep1 MigrateSchema()
        {
            schemaMigrationDefinition = new SchemaMigrationDefinition();
            return schemaMigrationDefinition;
        }

        public DocumentMigrationDefinition.IDocumentMigrationBuilderStep1 MigrateDocument()
        {
            documentMigrationDefinition = new DocumentMigrationDefinition();
            return documentMigrationDefinition;
        }


        public class SchemaMigrationDefinition : SchemaMigrationDefinition.ISchemaMigrationBuilderStep1,
                                                 SchemaMigrationDefinition.ISchemaMigrationBuilderStep2
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

            ISchemaMigrationBuilderStep2 ISchemaMigrationBuilderStep2.RewriteQuery(Func<string, string> rewrite)
            {
                Rewrite = rewrite;
                return this;
            }

            public interface ISchemaMigrationBuilderStep1
            {
                ISchemaMigrationBuilderStep2 ToVersion(int version);
            }

            public interface ISchemaMigrationBuilderStep2
            {
                ISchemaMigrationBuilderStep2 Migrate(Action<IMigrator> migration);
                ISchemaMigrationBuilderStep2 RewriteQuery(Func<string, string> rewrite);
            }
        }

        public class DocumentMigrationDefinition : DocumentMigrationDefinition.IDocumentMigrationBuilderStep1,
                                                   DocumentMigrationDefinition.IDocumentMigrationBuilderStep2,
                                                   DocumentMigrationDefinition.IDocumentMigrationBuilderStep3,
                                                   DocumentMigrationDefinition.IDocumentMigrationBuilderStep4,
                                                   DocumentMigrationDefinition.IDocumentMigrationBuilderStep5
        {
            public int RequiredSchemaVersion { get; private set; }
            public int Version { get; private set; }
            public string Tablename { get; private set; }
            public ISerializer Serializer { get; private set; }
            protected Action<object> MigrationOnRead { get; private set; }
            protected Action<object, IDictionary<string, object>> MigrationOnWrite { get; private set; }

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

            IDocumentMigrationBuilderStep5 IDocumentMigrationBuilderStep5.MigrateOnRead(Action<object> migration)
            {
                MigrationOnRead = migration;
                return this;
            }

            IDocumentMigrationBuilderStep5 IDocumentMigrationBuilderStep5.MigrateOnWrite(Action<object, IDictionary<string, object>> migration)
            {
                MigrationOnWrite = migration;
                return this;
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
                IDocumentMigrationBuilderStep5 MigrateOnRead(Action<object> migration);
                IDocumentMigrationBuilderStep5 MigrateOnWrite(Action<object, IDictionary<string, object>> migration);
            }
        }
    }

    public class Migration01
    {
        
        
        
        //step 0 - migrates to schemaversion check
        //step 1 - schema migrations
        //step 2 - vælg dokumenttype
        //step 3 - udfør json migration
        //step 4 - opdater versionnr
    }
}
