using System;
using HybridDb.Config;

namespace HybridDb.Migrations.Schema.Commands
{
    public class RemoveColumn : DdlCommand
    {
        public RemoveColumn(Table table, string name)
        {
            if (table.BuiltInColumns.ContainsKey(name))
                throw new InvalidOperationException($"You can not remove build in column {name}.");

            Safe = false;

            Table = table;
            Name = name;
        }

        public Table Table { get; }
        public string Name { get; }

        public override string ToString() => $"Remove column {Name} from table {Table.Name}";

        public override void Execute(DocumentStore store)
        {
            // TODO: sletter kun den første ser det ud til?
            var dropConstraints = new SqlBuilderOld()
                .Append("DECLARE @ConstraintName nvarchar(200)")
                .Append("SELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS ")
                .Append($"WHERE PARENT_OBJECT_ID = OBJECT_ID('{Table.Name}') ")
                .Append($"AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = N'{Name}' AND object_id = OBJECT_ID(N'{Table.Name}'))")
                .Append($"IF @ConstraintName IS NOT NULL ")
                .Append($"EXEC('ALTER TABLE {Table.Name} DROP CONSTRAINT ' + @ConstraintName)");

            store.Database.RawExecute(dropConstraints.ToString());

            store.Database.RawExecute($"alter table {store.Database.FormatTableNameAndEscape(Table.Name)} drop column {store.Database.Escape(Name)};");
        }
    }
}