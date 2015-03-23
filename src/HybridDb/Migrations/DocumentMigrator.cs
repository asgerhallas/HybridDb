using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations
{
    public class DocumentMigrator
    {
        readonly IDocumentStore store;

        public DocumentMigrator(IDocumentStore store)
        {
            this.store = store;
        }

        public object DeserializeAndMigrate(DocumentDesign design, byte[] document, int currentDocumentVersion)
        {
            var configuredVersion = store.Configuration.ConfiguredVersion;
            if (configuredVersion == currentDocumentVersion)
            {
                return store.Configuration.Serializer.Deserialize(document, design.DocumentType);
            }

            if (configuredVersion < currentDocumentVersion)
            {
                throw new InvalidOperationException(string.Format(
                    "Document version is ahead of configuration. Document is version {0}, but configuration is version {1}.",
                    currentDocumentVersion, configuredVersion));
            }

            foreach (var migration in store.Configuration.Migrations.Where(x => x.Version > currentDocumentVersion))
            {
                var commands = migration.MigrateDocument();
                foreach (var command in commands.OfType<ChangeDocument>().Where(x => x.ForType(design.DocumentType)))
                {
                    document = command.Execute(store.Configuration.Serializer, document);
                }
            }

            return store.Configuration.Serializer.Deserialize(document, design.DocumentType);
        }
    }
}