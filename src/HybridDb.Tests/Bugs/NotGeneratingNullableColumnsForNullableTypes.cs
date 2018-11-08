using System;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class NotGeneratingNullableColumnsForNullableTypes : HybridDbTests
    {
        [Fact]
        public void NullableGuidGetsNullableColumnType()
        {
            var store = DocumentStore.ForTesting(TableMode.UseLocalTempTables, connectionString, c => 
                c.Document<Entity>().With(x => x.SomeNullableGuid));

            store.Initialize();

            var column = store.Configuration.GetDesignFor<Entity>().Table["SomeNullableGuid"];
            column.Nullable.ShouldBe(true);
        }

        public class Entity
        {
            public Guid? SomeNullableGuid { get; set; }
        }
    }
}