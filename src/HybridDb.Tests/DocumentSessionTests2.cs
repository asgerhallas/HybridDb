using System;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentSessionTests2 : HybridDbTests
    {
        [Fact]
        public void CanNotOpenSessionBEforeStoreIsInitialized()
        {
            Should.Throw<InvalidOperationException>(() => store.OpenSession());
        }
    }
}