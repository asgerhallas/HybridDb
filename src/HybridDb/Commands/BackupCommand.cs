using System;
using HybridDb.Config;
using HybridDb.Migrations;

namespace HybridDb.Commands
{
    public class BackupCommand : DatabaseCommand
    {
        readonly DatabaseCommand command;
        readonly IBackupWriter writer;
        readonly DocumentDesign design;
        readonly string key;
        readonly int version;
        readonly byte[] document;

        public BackupCommand(DatabaseCommand command, IBackupWriter writer, DocumentDesign design, string key, int version, byte[] document)
        {
            this.command = command;
            this.writer = writer;
            this.design = design;
            this.key = key;
            this.version = version;
            this.document = document;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            writer.Write($"{design.DocumentType.FullName}_{key}_{version}.bak", document);

            return command.Prepare(store, etag, uniqueParameterIdentifier);
        }
    }
}