using HybridDb.Config;
using HybridDb.Events;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentStore_EventStoreTests : HybridDbAutoInitializeTests
    {
        [Fact]
        public void AppendEventCommand()
        {
            UseEventStore();

            store.Execute(new AppendEventCommand(new Table("events"), "testid", ""));
        }
    }
}