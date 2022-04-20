using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class DocumentMigrator
    {
        readonly ConcurrentDictionary<Table, IEnumerable<(Migration Migration, RowMigrationCommand Command)>> cache = new();

        readonly Configuration configuration;

        public DocumentMigrator(Configuration configuration) => this.configuration = configuration;

        public object DeserializeAndMigrate(IDocumentSession session, DocumentDesign design, IDictionary<string, object> row)
        {
            var migrations = cache.GetOrAdd(design.Table, key => configuration.Migrations
                .SelectMany(x => x.Background(configuration), (migration, command) => (Migration: migration, Command: command))
                .Where(x => x.Command.Matches(configuration, design.Table)));

            var migratedRow = migrations
                .Aggregate(row, (currentRow, nextCommand) =>
                    nextCommand.Command.Matches(nextCommand.Migration.Version, configuration, design, currentRow)
                        ? nextCommand.Command.Execute(session, configuration.Serializer, currentRow)
                        : currentRow);

            var currentDocumentVersion = row.Get(DocumentTable.VersionColumn);

            var configuredVersion = configuration.ConfiguredVersion;

            if (configuredVersion < currentDocumentVersion)
            {
                throw new InvalidOperationException(
                    $"Document {design.DocumentType.FullName}/{row.Get(DocumentTable.IdColumn)} version is ahead of configuration. " +
                    $"Document is version {currentDocumentVersion}, but configuration is version {configuredVersion}.");
            }

            var document = migratedRow.Get(DocumentTable.DocumentColumn);
            return document == null ? null : configuration.Serializer.Deserialize(document, design.DocumentType);
        }
    }
}