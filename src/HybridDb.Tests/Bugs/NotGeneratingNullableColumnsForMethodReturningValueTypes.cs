using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class NotGeneratingNullableColumnsForMethodReturningValueTypes
    {
        [Fact]
        public void Fails()
        {
            var store = DocumentStore.ForTesting(
                TableMode.UseTempTables,
                configurator: new LambdaHybridDbConfigurator(config => config.Document<Entity>().With(x => x.SomeEnumerable.Count())));

            var column = store.Configuration.GetDesignFor<Entity>().Table["SomeEnumerableCount"];
            column.SqlColumn.Nullable.ShouldBe(true);
        }

        public class Entity
        {
            public IEnumerable<object> SomeEnumerable { get; set; }
        }
    }
}