using System;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Bugs
{
    public class NotGeneratingNullableColumnsForNullableTypes : HybridDbTests
    {
        public NotGeneratingNullableColumnsForNullableTypes(ITestOutputHelper output) : base(output) { }

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