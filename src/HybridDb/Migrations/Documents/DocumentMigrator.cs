using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class DocumentMigrator
    {
        readonly ConcurrentDictionary<Table, IReadOnlyList<(Migration Migration, RowMigrationCommand Command)>> cache = new();

        readonly Configuration configuration;

        public DocumentMigrator(Configuration configuration) => this.configuration = configuration;

        public object DeserializeAndMigrate(IDocumentSession session, DocumentDesign design, IDictionary<string, object> row)
        {
            var migrations = cache.GetOrAdd(design.Table, key => configuration.Migrations
                .SelectMany(x => x.Background(configuration), (migration, command) => (Migration: migration, Command: command))
                .Where(x => x.Command.Matches(configuration, design.Table))
                .ToList());

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

            return document switch
            {
                DeletedDocument.Identifier => new DeletedDocument(),
                _ => configuration.Serializer.Deserialize(document, design.DocumentType)
            };
        }

        public class DeletedDocument
        {
            public const string Identifier = "###C67AF9B9-0561-4515-AC92-FC38FDECB07A";
        }
    }
}