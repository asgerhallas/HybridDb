using HybridDb.Config;

namespace HybridDb.Migrations.Schema.Commands
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
}