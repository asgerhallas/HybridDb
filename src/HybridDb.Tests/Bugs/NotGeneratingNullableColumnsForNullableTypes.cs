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
            Document<Entity>().With(x => x.SomeNullableGuid);

            store.Configuration.GetDesignFor<Entity>().Table["SomeNullableGuid"]
                .Nullable.ShouldBe(true);
        }

        public new class Entity
        {
            public Guid? SomeNullableGuid { get; set; }
        }
    }
}