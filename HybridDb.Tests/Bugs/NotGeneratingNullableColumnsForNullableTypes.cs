using System;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class NotGeneratingNullableColumnsForNullableTypes
    {
        [Fact]
        public void NullableGuidGetsNullableColumnType()
        {
            var store = DocumentStore.ForTestingWithTempTables();
            store.Document<Entity>().Project(x => x.SomeNullableGuid);
            var column = store.Configuration.GetDesignFor<Entity>().Table["SomeNullableGuid"];
            column.SqlColumn.Nullable.ShouldBe(true);
        }

        public class Entity
        {
            public Guid? SomeNullableGuid { get; set; }
        }
    }
}