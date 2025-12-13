using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Documentation
{
    public class Doc02_ContributingTests : DocumentationTestBase
    {
        public Doc02_ContributingTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void CanStoreAndLoadEntity()
        {
            #region CanStoreAndLoadEntity
            Document<Entity>();

            var entity = new Entity { Id = NewId(), Property = "Test" };
            
            using var session1 = store.OpenSession();

            session1.Store(entity);
            session1.SaveChanges();
            
            using var session2 = store.OpenSession();

            session2.Load<Entity>(entity.Id)
                .Property.ShouldBe("Test");
            #endregion
        }
    }
}
