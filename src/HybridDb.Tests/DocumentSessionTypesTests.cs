using System;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentSessionTypesTests : HybridDbAutoInitializeTests
    {
        [Fact]
        public async Task CanLoadByInterface()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id, Property = "Asger" });
                await session.SaveChangesAsync();
                session.Advanced.Clear();

                var entity1 = await session.LoadAsync<ISomeInterface>(id);
                entity1.ShouldBeOfType<MoreDerivedEntity1>();
                entity1.Property.ShouldBe("Asger");

                var entity2 = await session.LoadAsync<IOtherInterface>(id);
                entity2.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public async Task CanLoadDerivedEntityByBasetype()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id });
                await session.SaveChangesAsync();
                session.Advanced.Clear();

                var entity = await session.LoadAsync<AbstractEntity>(id);
                entity.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public async Task ThrowsOnLoadWhenFoundEntityDoesNotImplementInterface()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity2 { Id = id, Property = "Asger" });
                await session.SaveChangesAsync();
                session.Advanced.Clear();

                Should.Throw<InvalidOperationException>(() => session.LoadAsync<IOtherInterface>(id))
                    .Message.ShouldBe($"Document with id '{id}' exists, but is of type 'HybridDb.Tests.HybridDbTests+MoreDerivedEntity2', which is not assignable to 'HybridDb.Tests.HybridDbTests+IOtherInterface'.");
            }
        }

        [Fact]
        public async Task ThrowOnLoadWhenFoundEntityIsNotASubtype()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id });
                await session.SaveChangesAsync();
                session.Advanced.Clear();

                Should.Throw<InvalidOperationException>(() => session.LoadAsync<MoreDerivedEntity2>(id))
                    .Message.ShouldBe($"Document with id '{id}' exists, but is of type 'HybridDb.Tests.HybridDbTests+MoreDerivedEntity1', which is not assignable to 'HybridDb.Tests.HybridDbTests+MoreDerivedEntity2'.");
            }
        }

        [Fact]
        public async Task CanLoadDerivedEntityByOwnType()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id });
                await session.SaveChangesAsync();
                session.Advanced.Clear();

                var entity = await session.LoadAsync<MoreDerivedEntity1>(id);
                entity.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public async Task LoadByBasetypeCanReturnNull()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity = await session.LoadAsync<AbstractEntity>(id);
                entity.ShouldBe(null);
            }
        }

        [Fact]
        public async Task CanQueryByInterface()
        {
            Document<AbstractEntity>().With(x => x.Property);
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = NewId(), Property = "Asger" });
                session.Store(new MoreDerivedEntity2 { Id = NewId(), Property = "Asger" });
                await session.SaveChangesAsync();
                session.Advanced.Clear();

                var entities = session.Query<ISomeInterface>().OrderBy(x => QueryableEx.Column<string>(x, "Discriminator")).ToList();
                entities.Count.ShouldBe(2);
                entities[0].ShouldBeOfType<MoreDerivedEntity1>();
                entities[1].ShouldBeOfType<MoreDerivedEntity2>();

                var entities2 = session.Query<IOtherInterface>().OrderBy(x => x.Column<string>("Discriminator")).ToList();
                entities2.Count.ShouldBe(1);
                entities[0].ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public async Task CanQueryByBasetype()
        {
            Document<AbstractEntity>().With(x => x.Property);
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = NewId(), Property = "Asger" });
                session.Store(new MoreDerivedEntity2 { Id = NewId(), Property = "Asger" });
                await session.SaveChangesAsync();
                session.Advanced.Clear();

                var entities = session.Query<AbstractEntity>().Where(x => x.Property == "Asger").OrderBy(x => x.Column<string>("Discriminator")).ToList();
                entities.Count.ShouldBe(2);
                entities[0].ShouldBeOfType<MoreDerivedEntity1>();
                entities[1].ShouldBeOfType<MoreDerivedEntity2>();
            }
        }

        [Fact]
        public async Task CanQueryBySubtype()
        {
            Document<AbstractEntity>().With(x => x.Property);
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = NewId(), Property = "Asger" });
                session.Store(new MoreDerivedEntity2 { Id = NewId(), Property = "Asger" });
                await session.SaveChangesAsync();
                session.Advanced.Clear();

                var entities = session.Query<MoreDerivedEntity2>().Where(x => x.Property == "Asger").ToList();
                entities.Count.ShouldBe(1);
                entities[0].ShouldBeOfType<MoreDerivedEntity2>();
            }
        }

        [Fact]
        public async Task CanLoadSubTypeWhenOnlyRegisteringBaseType()
        {
            Document<AbstractEntity>();

            using (var session = store.OpenSession())
            {
                session.Store("key", new DerivedEntity());
                await session.SaveChangesAsync();
            }

            Reset();

            Document<AbstractEntity>();

            using (var session = store.OpenSession())
            {
                var load = await session.LoadAsync<AbstractEntity>("key");
                load.ShouldBeOfType<DerivedEntity>();
            }
        }

        [Fact]
        public async Task CanLoadSubTypeWhenRegisteringNoTypes()
        {
            using (var session = store.OpenSession())
            {
                session.Store("key", new DerivedEntity());
                await session.SaveChangesAsync();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                var load = await session.LoadAsync<AbstractEntity>("key");
                load.ShouldBeOfType<DerivedEntity>();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                var load = await session.LoadAsync<object>("key");
                load.ShouldBeOfType<DerivedEntity>();
            }
        }

        [Fact]
        public async Task CanQueryOnUnknownSubtypesOfObject()
        {
            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Id = "A" });
                session.Store(new MoreDerivedEntity1 { Id = "B" });
                session.Store(new OtherEntity { Id = "C" }); // unrelated type
                await session.SaveChangesAsync();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                // filters out results that is not subtype of DerivedEntity
                var list = session.Query<DerivedEntity>().ToList();
                list.Count.ShouldBe(2);
                list[0].Id.ShouldBe("A");
                list[1].Id.ShouldBe("B");
            }
        }

        [Fact]
        public async Task CanQueryOnUnknownSubtypesOfRegisteredType()
        {
            Document<DerivedEntity>();

            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Id = "A" });
                session.Store(new MoreDerivedEntity1 { Id = "B" });
                session.Store(new OtherEntity { Id = "C" }); // unrelated type
                await session.SaveChangesAsync();
            }

            Reset();

            Document<DerivedEntity>();

            using (var session = store.OpenSession())
            {
                // filters out results that is not subtype of DerivedEntity
                var list = session.Query<DerivedEntity>().ToList();
                list.Count.ShouldBe(2);
                list[0].Id.ShouldBe("A");
                list[1].Id.ShouldBe("B");
            }
        }

        [Fact]
        public async Task CanQueryOnUnknownSubtypesOfObjectByInterface()
        {
            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Id = "A", Property = "A" });
                session.Store(new MoreDerivedEntity1 { Id = "B", Property = "B" });
                session.Store(new OtherEntity { Id = "C" }); // does not implement interface
                await session.SaveChangesAsync();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                // filters out results that does not match the interface
                var list = session.Query<ISomeInterface>().ToList();
                list.Count.ShouldBe(2);
                list[0].Property.ShouldBe("A");
                list[1].Property.ShouldBe("B");
            }
        }

        [Fact]
        public async Task CanStoreAndLoadAnonymousObject()
        {
            var entity = new { SomeString = "Asger" };

            using (var session = store.OpenSession())
            {
                session.Store("key", entity);
                await session.SaveChangesAsync();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                var load = await session.LoadAsync<object>("key");
                load.ShouldBeOfType(entity.GetType());
            }
        }

        [Fact]
        public async Task CanStoreAndLoadAnonymousObjectByPrototype()
        {
            var entity = new { SomeString = "Asger" };

            using (var session = store.OpenSession())
            {
                session.Store("key", entity);
                await session.SaveChangesAsync();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                var load = await session.LoadAsync(entity.GetType(), "key");
                load.ShouldBeOfType(entity.GetType());
            }
        }

        [Fact]
        public async Task AutoRegistersSubTypesOnStore()
        {
            Document<AbstractEntity>().With(x => x.Property);

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = "A", Property = "Asger" });
                await session.SaveChangesAsync();

                configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldNotBe(null);

                session.Query<AbstractEntity>().Single(x => x.Property == "Asger")
                    .ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public async Task AutoRegistersSubTypesOnLoad()
        {
            Document<AbstractEntity>().With(x => x.Property);

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = "A", Property = "Asger" });
                await session.SaveChangesAsync();
            }

            Reset();

            Document<AbstractEntity>().With(x => x.Property);

            using (var session = store.OpenSession())
            {
                (await session.LoadAsync<AbstractEntity>("A")).ShouldBeOfType<MoreDerivedEntity1>();

                configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldNotBe(null);
            }
        }

        [Fact]
        public async Task AutoRegistersSubTypesOnQuery()
        {
            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Id = "A" });
                session.Store(new MoreDerivedEntity1 { Id = "B" });
                session.Store(new OtherEntity { Id = "C" }); // unrelated type
                await session.SaveChangesAsync();
            }

            Reset();

            configuration.TryGetDesignFor(typeof(DerivedEntity)).ShouldBe(null);
            configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldBe(null);
            configuration.TryGetDesignFor(typeof(OtherEntity)).ShouldBe(null);

            using (var session = store.OpenSession())
            {
                // filters out results that is not subtype of DerivedEntity
                session.Query<DerivedEntity>().Statistics(out var stats).ToList().Count.ShouldBe(2);
                stats.TotalResults.ShouldBe(3);
            }

            configuration.TryGetDesignFor(typeof(DerivedEntity)).ShouldNotBe(null);
            configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldNotBe(null);
            configuration.TryGetDesignFor(typeof(OtherEntity)).ShouldNotBe(null);

            using (var session = store.OpenSession())
            {
                // second time around it should filter by discriminators
                session.Query<DerivedEntity>().Statistics(out var stats).ToList().Count.ShouldBe(2);
                stats.TotalResults.ShouldBe(2);
            }
        }

        [Fact]
        public async Task AutoRegistersSubTypesOnQueryForProjections()
        {
            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Id = "A" });
                session.Store(new MoreDerivedEntity1 { Id = "B" });
                session.Store(new OtherEntity { Id = "C" }); // unrelated type
                await session.SaveChangesAsync();
            }

            Reset();

            configuration.TryGetDesignFor(typeof(DerivedEntity)).ShouldBe(null);
            configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldBe(null);
            configuration.TryGetDesignFor(typeof(OtherEntity)).ShouldBe(null);

            using (var session = store.OpenSession())
            {
                // filters out results that is not subtype of DerivedEntity
                session.Query<DerivedEntity>()
                    .Statistics(out var stats)
                    .Select(x => new { x.Id }).ToList()
                    .Count.ShouldBe(2);

                stats.TotalResults.ShouldBe(3);
            }

            configuration.TryGetDesignFor(typeof(DerivedEntity)).ShouldNotBe(null);
            configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldNotBe(null);
            configuration.TryGetDesignFor(typeof(OtherEntity)).ShouldNotBe(null);

            using (var session = store.OpenSession())
            {
                // second time around it should filter by discriminators
                session.Query<DerivedEntity>()
                    .Statistics(out var stats)
                    .Select(x => new { x.Id }).ToList()
                    .Count.ShouldBe(2);

                stats.TotalResults.ShouldBe(2);
            }
        }

        [Fact]
        public async Task FailsIfTypeMapperCantMapToConcreteType()
        {
            UseTypeMapper(new FailingTypeMapper());

            using (var session = store.OpenSession())
            {
                session.Store("key", new DerivedEntity());
                await session.SaveChangesAsync();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                Should.Throw<InvalidOperationException>(() => session.LoadAsync<AbstractEntity>("key"))
                    .Message.ShouldBe("No concrete type could be mapped from discriminator 'NoShow'.");
            }
        }

        public class FailingTypeMapper : ITypeMapper
        {
            public string ToDiscriminator(Type type) => "NoShow";

            public Type ToType(string discriminator) => null;
        }
    }
}