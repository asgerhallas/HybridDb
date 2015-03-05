using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;
using System.Linq;

namespace HybridDb.Tests.Bugs
{
    public class NotGeneratingNullableColumnsForNullableTypes
    {
        [Fact]
        public void NullableGuidGetsNullableColumnType()
        {
            var store = DocumentStore.ForTestingWithTempTables();
            store.Document<Entity>().With(x => x.SomeNullableGuid);
            var column = store.Configuration.GetDesignFor<Entity>().Table["SomeNullableGuid"];
            column.SqlColumn.Nullable.ShouldBe(true);
        }

        public class Entity
        {
            public Guid? SomeNullableGuid { get; set; }
        }
    }

    public class NotGeneratingNullableColumnsForMethodReturningValueTypes
    {
        [Fact]
        public void Fails()
        {
            var store = DocumentStore.ForTestingWithTempTables();
            store.Document<Entity>().With(x => x.SomeEnumerable.Count());
            var column = store.Configuration.GetDesignFor<Entity>().Table["SomeEnumerableCount"];
            column.SqlColumn.Nullable.ShouldBe(true);
        }

        public class Entity
        {
            public IEnumerable<object> SomeEnumerable { get; set; }
        }
    }
}