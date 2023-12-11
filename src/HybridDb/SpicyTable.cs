using HybridDb.Config;

namespace HybridDb
{
    public class SpicyTable
    {
        public SpicyTable(IDocumentStore store, Table table)
        {
            TableName = store.Database.FormatTableNameAndEscape(table.Name);
            if (table.IsCreated) return;
            store.Execute(table.GetCreateCommand());
        }

        public string TableName { get; }

        public override string ToString() => TableName;
    }
}
