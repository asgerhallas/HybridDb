using HybridDb.Config;
using HybridDb.Events.Commands;
using HybridDb.Migrations.Schema;

namespace HybridDb.Events
{
    public class EventTable : Table
    {
        public EventTable(string name) : base(name) { }

        public override SchemaMigrationCommand GetCreateCommand() => new CreateEventTable(this);
    }
}