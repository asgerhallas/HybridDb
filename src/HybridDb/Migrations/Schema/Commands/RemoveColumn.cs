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
            var tableName = store.Database.FormatTableName(Table.Name);
            var escapedTableName = store.Database.FormatTableNameAndEscape(Table.Name);

            var dropConstraints = Sql.From($@"
                DECLARE @ConstraintName nvarchar(200)
                SELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS
                WHERE PARENT_OBJECT_ID = OBJECT_ID({tableName})
                AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = {Name} AND object_id = OBJECT_ID({tableName}))
                IF @ConstraintName IS NOT NULL
                EXEC('ALTER TABLE {escapedTableName:verbatim} DROP CONSTRAINT ' + @ConstraintName)");

            store.Database.RawExecute(dropConstraints);

            store.Database.RawExecute(Sql.From($"alter table {Table} drop column {new Column<string>(Name)};"));
        }
    }
}