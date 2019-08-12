using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class DocumentMigrator
    {
        readonly Configuration configuration;

        public DocumentMigrator(Configuration configuration) => this.configuration = configuration;

        public object DeserializeAndMigrate(IDocumentSession session, DocumentDesign design, string id, IDictionary<string, object> row)
        {
            // TODO: This can be optimized to not find and filter the migrations for every row
            var migratedRow = configuration.Migrations
                .SelectMany(x => x.Background(configuration), (migration, command) => (Migration: migration, Command: command))
                .Where(x => x.Command.Matches(configuration, design.Table))
                .Aggregate(row, (currentRow, nextCommand) =>
                    nextCommand.Command.Matches(nextCommand.Migration.Version, configuration, currentRow)
                        ? nextCommand.Command.Execute(session, configuration.Serializer, currentRow)
                        : currentRow);

            var currentDocumentVersion = row.Get(DocumentTable.VersionColumn);

            var configuredVersion = configuration.ConfiguredVersion;

            if (configuredVersion < currentDocumentVersion)
            {
                throw new InvalidOperationException(
                    $"Document {design.DocumentType.FullName}/{id} version is ahead of configuration. " +
                    $"Document is version {currentDocumentVersion}, but configuration is version {configuredVersion}.");
            }

            var document = migratedRow.Get(DocumentTable.DocumentColumn);

            return configuration.Serializer.Deserialize(document, design.DocumentType);
        }
    }
}