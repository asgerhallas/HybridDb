using System;
using System.Linq;
using HybridDb.Config;
using Serilog;

namespace HybridDb.Migrations
{
    public class DocumentMigrator
    {
        readonly Configuration configuration;
        readonly ILogger logger;

        public DocumentMigrator(Configuration configuration)
        {
            this.configuration = configuration;
            logger = configuration.Logger;
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
                    "Document {0}/{1} version is ahead of configuration. Document is version {2}, but configuration is version {3}.",
                    design.DocumentType.FullName, id, currentDocumentVersion, configuredVersion));
            }

            logger.Information("Migrating document {0}/{1} from version {2} to {3}.", 
                design.DocumentType.FullName, id, currentDocumentVersion, configuration.ConfiguredVersion);

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