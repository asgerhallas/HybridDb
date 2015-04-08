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
        readonly Guid id;
        readonly int version;
        readonly byte[] document;

        public BackupCommand(DatabaseCommand command, IBackupWriter writer, DocumentDesign design, Guid id, int version, byte[] document)
        {
            this.command = command;
            this.writer = writer;
            this.design = design;
            this.id = id;
            this.version = version;
            this.document = document;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var name = string.Format("{0}_{1}_{2}.bak", design.DocumentType.FullName, id, version);
            writer.Write(name, document);

            return command.Prepare(store, etag, uniqueParameterIdentifier);
        }
    }
}