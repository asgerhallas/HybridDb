using HybridDb.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Commands
{
    public class GetCommandTests(ITestOutputHelper output) : HybridDbTests(output)
    {
        [Fact]
        public void CanGetSingleDocument()
        {
            Document<Entity>();
            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();
            store.Insert(table, id, new { });

            var result = store.Execute(new GetCommand(table, [id]));

            result.ShouldContainKey(id);
        }

        [Fact]
        public void CanGetMultipleDocuments()
        {
            Document<Entity>();
            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id1 = NewId();
            var id2 = NewId();
            store.Insert(table, id1, new { });
            store.Insert(table, id2, new { });

            var result = store.Execute(new GetCommand(table, [id1, id2]));

            result.Count.ShouldBe(2);
            result.ShouldContainKey(id1);
            result.ShouldContainKey(id2);
        }

        [Fact]
        public void GetReturnsNothingForMissingId()
        {
            Document<Entity>();
            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var result = store.Execute(new GetCommand(table, [NewId()]));

            result.ShouldBeEmpty();
        }
    }
}
