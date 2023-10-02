using System;
using Microsoft.Data.SqlClient;
using System.Linq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class DocumentSession_IdCaseSensivityTests : HybridDbTests
    {
        public DocumentSession_IdCaseSensivityTests(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData("asger")]
        [InlineData("ASGER")]
        [InlineData("AsgeR")]
        public void Ids_CaseInsensitive_Load(string id)
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity
                {
                    Id = id
                });

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity1 = session.Load<Entity>("ASGER");
                var entity2 = session.Load<Entity>("AsgeR");
                var entity3 = session.Load<Entity>("asger");

                entity1.Id.ShouldBe(id);
                session.Advanced.ManagedEntities.Count.ShouldBe(1);
                session.Advanced.ManagedEntities.Single().Key.Id.ShouldBe(id);

                ReferenceEquals(entity1, entity2).ShouldBe(true);
                ReferenceEquals(entity2, entity3).ShouldBe(true);
            }
        }        
        
        [Fact]
        public void Ids_CaseInsensitive_StoreLoad_SameSession()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                var saved = new Entity
                {
                    Id = "asger"
                };

                session.Store(saved);

                var loaded = session.Load<Entity>("ASGER");

                ReferenceEquals(saved, loaded).ShouldBe(true);

                session.Advanced.ManagedEntities.Count.ShouldBe(1);
            }
        }        
        
        [Fact]
        public void Ids_CaseInsensitive_Store()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = "asger" });
                Should.Throw<HybridDbException>(() => session.Store(new Entity { Id = "Asger" }));

                session.Advanced.ManagedEntities.Count.ShouldBe(1);
                session.Advanced.ManagedEntities.Single().Value.Key.ShouldBe("asger");
            }
        }

        [Fact]
        public void Ids_CaseInsensitive_SaveChanges()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = "asger" });
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = "ASGER" });
                Should.Throw<SqlException>(() => session.SaveChanges());
            }
        }

        [Fact]
        public void Ids_CaseInsensitive_GivenIdAndIdProperty()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store("peter", new Entity { Id = "asger" });
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                session.Load<Entity>("peter").Id.ShouldBe("asger");
                session.Load<Entity>("asger").ShouldBe(null);
            }
        }

        [Fact]
        public void Ids_CaseInsensitive_GivenIdAndIdProperty_Evict()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = "asger" };
                session.Store("peter", entity);
                session.Advanced.Evict(entity);
                session.Advanced.ManagedEntities.ShouldBeEmpty();
            }
        }

        [Fact]
        public void Ids_CaseInsensitive_GivenIdAndIdProperty_Delete()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = "asger" });
                session.Store(new Entity { Id = "peter" });
                session.SaveChanges();
                session.Advanced.Clear();

                var loaded = session.Load<Entity>("asger");
                loaded.Id = "peter";
                session.Delete(loaded);
                session.SaveChanges();
                session.Advanced.Clear();

                session.Load<Entity>("peter").ShouldNotBe(null);
                session.Load<Entity>("asger").ShouldBe(null);
            }
        }

        [Fact]
        public void Ids_CaseInsensitive_LoadAndChangeId_DifferentCase()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = "asger" });
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>("asger");
                entity.Id = "ASGER";

                session.Store(entity);
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                session.Load<Entity>("asger").Id.ShouldBe("ASGER");
            }
        }

        [Fact]
        public void Ids_CaseInsensitive_LoadAndChangeId()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = "asger" });
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var entity = session.Load<Entity>("asger");
                entity.Id = "peter";

                Should.Throw<HybridDbException>(() => session.Store(entity));
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                session.Load<Entity>("asger").Id.ShouldBe("peter");
            }
        }

        [Fact]
        public void Ids_CaseInsensitive_StoreSameInstanceTwice_DifferentIds()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = "asger" };
                session.Store(entity);

                entity.Id = "peter";
                Should.Throw<HybridDbException>(() => session.Store(entity));
                session.Advanced.ManagedEntities.Count.ShouldBe(1);
            }
        }

        [Fact]
        public void Ids_CaseInsensitive_StoreSameInstanceTwice_SameIds_DifferentCase()
        {
            Document<Entity>();

            using (var session = store.OpenSession())
            {
                var entity = new Entity { Id = "asger" };
                session.Store(entity);

                entity.Id = "ASGER";
                session.Store(entity);
                session.Advanced.ManagedEntities.Count.ShouldBe(1);
            }
        }
    }
}