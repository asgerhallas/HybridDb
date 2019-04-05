using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb.Events
{
    public class AppendEventCommand : DatabaseCommand
    {
        public AppendEventCommand(Table table, string streamId, object projections)
        {
            Table = table;
            StreamId = streamId;
            Projections = projections;
        }

        public string StreamId { get; }
        public object Projections { get; }
        public Table Table { get; }
    }
}