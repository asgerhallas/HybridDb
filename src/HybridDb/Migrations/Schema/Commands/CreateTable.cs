using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Schema.Commands
{
    public class CreateTable : DdlCommand
    {
        public CreateTable(Table table) => Table = table;

        public Table Table { get; }

        public override string ToString() => $"Create table {Table} with columns {string.Join(", ", Table.Columns.Select(x => x.ToString()))}";
    }
}