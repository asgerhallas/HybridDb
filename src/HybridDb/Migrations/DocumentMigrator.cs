using System;
using System.Collections.Generic;
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

        public IEnumerable<DocumentMigrationCommand> ApplicableCommands(DocumentDesign design, int currentDocumentVersion)
        {
            return configuration.Migrations
                .Where(x => x.Version > currentDocumentVersion)
                .SelectMany(x => x.MigrateDocument())
                .Where(x => x.ForType(design.DocumentType));
        }

        public object DeserializeAndMigrate(IDocumentSession session, DocumentDesign design, string id, byte[] document, int currentDocumentVersion)
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

            document = ApplicableCommands(design, currentDocumentVersion)
                .Aggregate(document, (currentDocument, command) => 
                    command.Execute(session, configuration.Serializer, currentDocument));

            return configuration.Serializer.Deserialize(document, design.DocumentType);
        }
    }
}