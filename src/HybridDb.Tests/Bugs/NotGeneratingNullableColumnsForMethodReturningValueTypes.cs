using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class NotGeneratingNullableColumnsForMethodReturningValueTypes : HybridDbTests
    {
        [Fact]
        public void Fails()
        {
            var store = DocumentStore.ForTesting(
                TableMode.UseTempTables,
                connectionString,
                new LambdaHybridDbConfigurator(config => config.Document<Entity>().With(x => x.SomeEnumerable.Count())));

            var column = store.Configuration.GetDesignFor<Entity>().Table["SomeEnumerableCount"];
            column.Nullable.ShouldBe(true);
        }

        public class Entity
        {
            public IEnumerable<object> SomeEnumerable { get; set; }
        }
    }
}