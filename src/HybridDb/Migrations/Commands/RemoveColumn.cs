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

        public override void Execute(IDatabase database)
        {
            var tableName = database.FormatTableNameAndEscape(Table.Name);
            var dropConstraints = "DECLARE @ConstraintName nvarchar(200) " +
                                  "SELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS " +
                                  "WHERE PARENT_OBJECT_ID = OBJECT_ID('" + tableName + "') " +
                                  "AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = N'" + Name + "' AND object_id = OBJECT_ID(N'" + tableName + "')) " +
                                  "IF @ConstraintName IS NOT NULL " +
                                  "EXEC('ALTER TABLE " + tableName + " DROP CONSTRAINT ' + @ConstraintName) ";

            database.RawExecute(dropConstraints);
            database.RawExecute($"alter table {database.FormatTableNameAndEscape(Table.Name)} drop column {database.Escape(Name)};");
        }

        public override string ToString() => $"Remove column {Name} from table {Table.Name}";
    }
}