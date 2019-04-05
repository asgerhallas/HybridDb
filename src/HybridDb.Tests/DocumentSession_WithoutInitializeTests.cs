using System;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentSession_WithoutInitializeTests : HybridDbTests
    {
        [Fact]
        public void CanNotOpenSessionBeforeStoreIsInitialized()
        {
            Should.Throw<InvalidOperationException>(() => store.OpenSession());
        }
    }
}