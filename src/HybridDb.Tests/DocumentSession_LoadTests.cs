using System.Linq;
using ShouldBeLike;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static HybridDb.Helpers;

namespace HybridDb.Tests
{
    public class DocumentSession_LoadTests : HybridDbTests
    {
        public DocumentSession_LoadTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Load_Multiple()
        {
            Document<Entity>();

            var id1 = NewId();
            var id2 = NewId();
            var id3 = NewId();
            
            using var session = store.OpenSession();
            
            session.Store(new Entity { Id = id1, Property = "Asger" });
            session.Store(new Entity { Id = id2, Property = "Lars" });
            session.Store(new Entity { Id = id3, Property = "Jacob" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entities = session.Load<Entity>(ListOf(id2, id3));
                
            entities.Select(x => x.Property).ShouldBeLikeUnordered("Lars", "Jacob");
        }

        [Fact]
        public void Load_Multiple_SessionCache()
        {
            Document<Entity>();

            var id1 = NewId();
            var id2 = NewId();
            var id3 = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity { Id = id1, Property = "Asger" });
            session.Store(new Entity { Id = id2, Property = "Lars" });
            session.Store(new Entity { Id = id3, Property = "Jacob" });
            session.SaveChanges();
            session.Advanced.Clear();

            var entity = session.Load<Entity>(id1);
             
            var entities = session.Load<Entity>(ListOf(id1, id2)).OrderBy(x => x.Property).ToList();

            entities.Select(x => x.Property).ShouldBeLike("Asger", "Lars");
            
            entities[0].ShouldBeSameAs(entity);
        }
    }
}