using System;
using HybridDb.Config;

namespace HybridDb.Migrations.Commands
{
    public class AddColumn : SchemaMigrationCommand
    {
        public AddColumn(string tablename, Column column)
        {
            RequiresReprojectionOf = tablename;

            Tablename = tablename;
            Column = column;
        }

        public string Tablename { get; }
        public Column Column { get; }

        public override string ToString() => $"Add column {Column} to table {Tablename}.";
    }

    public class AddColumnExecutor : DdlCommandExecutor<DocumentStore, AddColumn>
    {
        public override void Execute(DocumentStore store, AddColumn command)
        {
            store.Database.RawExecute(new SqlBuilder()
                .Append($"alter table {store.Database.FormatTableNameAndEscape(command.Tablename)} add {store.Database.Escape(command.Column.Name)}")
                .Append(store.BuildColumnSql(command.Column))
                .ToString());
        }

    }
}