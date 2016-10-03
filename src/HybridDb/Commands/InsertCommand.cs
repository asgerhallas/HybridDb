using HybridDb.Config;

namespace HybridDb.Commands
{
    public class InsertCommand : DatabaseCommand
    {
        public InsertCommand(DocumentTable table, string id, object projections)
        {
            Table = table;
            Id = id;
            Projections = projections;
        }

        public string Id { get; }
        public object Projections { get; }
        public DocumentTable Table { get; }
    }
}