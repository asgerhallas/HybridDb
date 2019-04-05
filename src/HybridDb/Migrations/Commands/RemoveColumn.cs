using System;
using HybridDb.Config;

namespace HybridDb.Migrations.Commands
{
    public class RemoveColumn : SchemaMigrationCommand
    {
        public RemoveColumn(Table table, string name)
        {
            Unsafe = true;

            Table = table;
            Name = name;
        }

        public Table Table { get; }
        public string Name { get; }

        public override string ToString() => $"Remove column {Name} from table {Table.Name}";
    }

    public class RemoveColumnExecutor : DdlCommandExecutor<DocumentStore, RemoveColumn>
    {
        public override void Execute(DocumentStore store, RemoveColumn command)
        {
            store.Database.RawExecute($"alter table {store.Database.FormatTableNameAndEscape(command.Table.Name)} drop column {store.Database.Escape(command.Name)};");
        }
    }
}