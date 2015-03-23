using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations
{
    public class DocumentMigrator
    {
        readonly Configuration configuration;

        public DocumentMigrator(Configuration configuration)
        {
            this.configuration = configuration;
        }

        public object DeserializeAndMigrate(DocumentDesign design, Guid id, byte[] document, int currentDocumentVersion)
        {
            var configuredVersion = configuration.ConfiguredVersion;
            if (configuredVersion == currentDocumentVersion)
            {
                return configuration.Serializer.Deserialize(document, design.DocumentType);
            }

            if (configuredVersion < currentDocumentVersion)
            {
                throw new InvalidOperationException(string.Format(
                    "Document version is ahead of configuration. Document is version {0}, but configuration is version {1}.",
                    currentDocumentVersion, configuredVersion));
            }

            configuration.BackupWriter.Write(design, id, currentDocumentVersion, document);

            foreach (var migration in configuration.Migrations.Where(x => x.Version > currentDocumentVersion))
            {
                var commands = migration.MigrateDocument();
                foreach (var command in commands.OfType<ChangeDocument>().Where(x => x.ForType(design.DocumentType)))
                {
                    document = command.Execute(configuration.Serializer, document);
                }
            }

            return configuration.Serializer.Deserialize(document, design.DocumentType);
        }
    }
}