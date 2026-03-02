using System;
using HybridDb.Config;
using HybridDb.SqlBuilder;

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
            var formattedTableName = store.Database.FormatTableName(Table.Name);
            var columnName = Name.Replace("'", "''");

            // TODO: sletter kun den første ser det ud til?
            var dropConstraints = Sql.Empty
                .Append("DECLARE @ConstraintName nvarchar(200)")
                .Append("SELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS ")
                .Append("WHERE PARENT_OBJECT_ID = OBJECT_ID('" + formattedTableName + "') ")
                .Append("AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = N'" + columnName + "' AND object_id = OBJECT_ID(N'" + formattedTableName + "'))")
                .Append($"IF @ConstraintName IS NOT NULL ")
                .Append("EXEC('ALTER TABLE " + formattedTableName + " DROP CONSTRAINT ' + @ConstraintName)");

            store.Database.RawExecute(dropConstraints);

            store.Database.RawExecute(Sql.From($"alter table {Table} drop column {new Column<string>(Name)};"));
        }
    }
}