using HybridDb.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Commands
{
    public class ExistsCommandTests(ITestOutputHelper output) : HybridDbTests(output)
    {
        [Fact]
        public void CanExists()
        {
            Document<Entity>();
            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();
            store.Insert(table, id, new { });

            var etag = store.Execute(new ExistsCommand(table, id));

            etag.ShouldNotBeNull();
        }

        [Fact]
        public void ExistsReturnsNullForMissingDocument()
        {
            Document<Entity>();
            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var etag = store.Execute(new ExistsCommand(table, NewId()));

            etag.ShouldBeNull();
        }
    }
}
