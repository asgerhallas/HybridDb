using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class DocumentSession_ExistsTests : HybridDbTests
    {
        public DocumentSession_ExistsTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Exists_NotInDb_NotInSession()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.SaveChanges();
            session.Advanced.Clear();

            session.Advanced.Exists<Entity>(id, out var etag).ShouldBe(false);
            etag.ShouldBe(null);
        }

        [Fact]
        public void Exists_InDb()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity {Id = id});
            var storedEtag = session.SaveChanges();
            session.Advanced.Clear();

            session.Advanced.Exists<Entity>(id, out var etag).ShouldBe(true);
            etag.ShouldBe(storedEtag);
        }

        [Fact]
        public void Exists_InDb_OtherType()
        {
            Document<Entity>();
            Document<OtherEntity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity {Id = id});
            session.SaveChanges();
            session.Advanced.Clear();

            session.Advanced.Exists<OtherEntity>(id, out var etag).ShouldBe(false);
            etag.ShouldBe(null);
        }

        [Fact]
        public void Exists_InDb_OtherId()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity {Id = id});
            session.SaveChanges();
            session.Advanced.Clear();

            session.Advanced.Exists<Entity>(NewId(), out var etag).ShouldBe(false);
            etag.ShouldBe(null);
        }

        [Fact]
        public void Exists_InDb_DerivedType()
        {
            Document<AbstractEntity>();
            Document<DerivedEntity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new DerivedEntity {Id = id});
            var savedEtag = session.SaveChanges();
            session.Advanced.Clear();

            session.Advanced.Exists<AbstractEntity>(id, out var etag).ShouldBe(true);
            etag.ShouldBe(savedEtag);
        }

        [Fact]
        public void Exists_InSession_Stored()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity {Id = id});

            session.Advanced.Exists<Entity>(id, out var etag).ShouldBe(true);
            etag.ShouldBe(null);
        }

        [Fact]
        public void Exists_NotInConfig_NotInSession_NotInDb()
        {
            var id = NewId();

            using var session = store.OpenSession();

            session.Advanced.Exists<Entity>(id, out var etag).ShouldBe(false);
            etag.ShouldBe(null);
        }

        [Fact]
        public void Exists_InSession_Stored_AndSavedInDb()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity {Id = id});
            var savedEtag = session.SaveChanges();

            var x = session.Advanced.DocumentStore.Stats.NumberOfCommands;

            session.Advanced.Exists<Entity>(id, out var etag).ShouldBe(true);
            etag.ShouldBe(savedEtag);
            session.Advanced.DocumentStore.Stats.NumberOfCommands.ShouldBe(x);
        }

        [Fact]
        public void Exists_InSession_Loaded()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity {Id = id});
            var savedEtag = session.SaveChanges();
            session.Advanced.Clear();
            session.Load<Entity>(id);

            var x = session.Advanced.DocumentStore.Stats.NumberOfCommands;

            session.Advanced.Exists<Entity>(id, out var etag).ShouldBe(true);
            etag.ShouldBe(savedEtag);
            session.Advanced.DocumentStore.Stats.NumberOfCommands.ShouldBe(x);
        }

        [Fact]
        public void Exists_InSession_DerivedType()
        {
            Document<AbstractEntity>();
            Document<DerivedEntity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new DerivedEntity {Id = id});

            session.Advanced.Exists<AbstractEntity>(id, out var etag).ShouldBe(true);
            etag.ShouldBe(null);
        }

        [Fact]
        public void Exists_InSession_OtherId()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();

            session.Store(new Entity {Id = id});

            session.Advanced.Exists<Entity>(NewId(), out var etag).ShouldBe(false);
            etag.ShouldBe(null);
        }
    }
}