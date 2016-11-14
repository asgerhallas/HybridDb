using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentSessionVarianceTests : HybridDbAutoInitializeTests
    {
        [Fact]
        public void CanLoadByInterface()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id, Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entity1 = session.Load<ISomeInterface>(id);
                entity1.ShouldBeOfType<MoreDerivedEntity1>();
                entity1.Property.ShouldBe("Asger");

                var entity2 = session.Load<IOtherInterface>(id);
                entity2.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void CanLoadDerivedEntityByBasetype()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id });
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Load<AbstractEntity>(id);
                entity.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void ThrowsOnLoadWhenFoundEntityDoesNotImplementInterface()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity2 { Id = id, Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                Should.Throw<InvalidOperationException>(() => session.Load<IOtherInterface>(id))
                    .Message.ShouldBe($"Document with id '{id}' exists, but is not assignable to the given type '{typeof(IOtherInterface).Name}'.");
            }
        }

        [Fact]
        public void ThrowOnLoadWhenFoundEntityIsNotASubtype()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id });
                session.SaveChanges();
                session.Advanced.Clear();

                Should.Throw<InvalidOperationException>(() => session.Load<MoreDerivedEntity2>(id))
                    .Message.ShouldBe($"Document with id '{id}' exists, but is not assignable to the given type 'MoreDerivedEntity2'.");
            }
        }

        [Fact]
        public void CanLoadDerivedEntityByOwnType()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = id });
                session.SaveChanges();
                session.Advanced.Clear();

                var entity = session.Load<MoreDerivedEntity1>(id);
                entity.ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void LoadByBasetypeCanReturnNull()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();

            var id = NewId();
            using (var session = store.OpenSession())
            {
                var entity = session.Load<AbstractEntity>(id);
                entity.ShouldBe(null);
            }
        }

        [Fact]
        public void CanQueryByInterface()
        {
            Document<AbstractEntity>().With(x => x.Property);
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = NewId(), Property = "Asger" });
                session.Store(new MoreDerivedEntity2 { Id = NewId(), Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<ISomeInterface>().OrderBy(x => QueryableEx.Column<string>(x, "Discriminator")).ToList();
                entities.Count.ShouldBe(2);
                entities[0].ShouldBeOfType<MoreDerivedEntity1>();
                entities[1].ShouldBeOfType<MoreDerivedEntity2>();

                var entities2 = session.Query<IOtherInterface>().OrderBy(x => x.Column<string>("Discriminator")).ToList();
                entities2.Count().ShouldBe(1);
                entities[0].ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void CanQueryByBasetype()
        {
            Document<AbstractEntity>().With(x => x.Property);
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = NewId(), Property = "Asger" });
                session.Store(new MoreDerivedEntity2 { Id = NewId(), Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<AbstractEntity>().Where(x => x.Property == "Asger").OrderBy(x => x.Column<string>("Discriminator")).ToList();
                entities.Count().ShouldBe(2);
                entities[0].ShouldBeOfType<MoreDerivedEntity1>();
                entities[1].ShouldBeOfType<MoreDerivedEntity2>();
            }
        }

        [Fact]
        public void CanQueryBySubtype()
        {
            Document<AbstractEntity>().With(x => x.Property);
            Document<MoreDerivedEntity1>();
            Document<MoreDerivedEntity2>();

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = NewId(), Property = "Asger" });
                session.Store(new MoreDerivedEntity2 { Id = NewId(), Property = "Asger" });
                session.SaveChanges();
                session.Advanced.Clear();

                var entities = session.Query<MoreDerivedEntity2>().Where(x => x.Property == "Asger").ToList();
                entities.Count.ShouldBe(1);
                entities[0].ShouldBeOfType<MoreDerivedEntity2>();
            }
        }

        [Fact]
        public void CanLoadSubTypeWhenOnlyRegisteringBaseType()
        {
            Document<AbstractEntity>();

            using (var session = store.OpenSession())
            {
                session.Store("key", new DerivedEntity());
                session.SaveChanges();
            }

            Reset();

            Document<AbstractEntity>();

            using (var session = store.OpenSession())
            {
                var load = session.Load<AbstractEntity>("key");
                load.ShouldBeOfType<DerivedEntity>();
            }
        }

        [Fact]
        public void CanLoadSubTypeWhenRegisteringNoTypes()
        {
            using (var session = store.OpenSession())
            {
                session.Store("key", new DerivedEntity());
                session.SaveChanges();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                var load = session.Load<AbstractEntity>("key");
                load.ShouldBeOfType<DerivedEntity>();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                var load = session.Load<object>("key");
                load.ShouldBeOfType<DerivedEntity>();
            }
        }

        [Fact]
        public void CanQueryOnUnknownSubtypesOfObject()
        {
            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Id = "A" });
                session.Store(new MoreDerivedEntity1 { Id = "B" });
                session.Store(new OtherEntity { Id = "C" }); // unrelated type
                session.SaveChanges();
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
        public void CanQueryOnUnknownSubtypesOfRegisteredType()
        {
            Document<DerivedEntity>();

            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Id = "A" });
                session.Store(new MoreDerivedEntity1 { Id = "B" });
                session.Store(new OtherEntity { Id = "C" }); // unrelated type
                session.SaveChanges();
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
        public void CanQueryOnUnknownSubtypesOfObjectByInterface()
        {
            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Id = "A", Property = "A" });
                session.Store(new MoreDerivedEntity1 { Id = "B", Property = "B" });
                session.Store(new OtherEntity { Id = "C" }); // does not implement interface
                session.SaveChanges();
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
        public void CanStoreAndLoadAnonymousObject()
        {
            var entity = new { SomeString = "Asger" };

            using (var session = store.OpenSession())
            {
                session.Store("key", entity);
                session.SaveChanges();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                var load = session.Load<object>("key");
                load.ShouldBeOfType(entity.GetType());
            }
        }

        [Fact]
        public void CanStoreAndLoadAnonymousObjectByPrototype()
        {
            var entity = new { SomeString = "Asger" };

            using (var session = store.OpenSession())
            {
                session.Store("key", entity);
                session.SaveChanges();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                var load = session.Load(entity, "key");
                load.ShouldBeOfType(entity.GetType());
            }
        }

        [Fact]
        public void AutoRegistersSubTypesOnStore()
        {
            Document<AbstractEntity>().With(x => x.Property);

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = "A", Property = "Asger" });
                session.SaveChanges();

                configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldNotBe(null);

                session.Query<AbstractEntity>().Single(x => x.Property == "Asger")
                    .ShouldBeOfType<MoreDerivedEntity1>();
            }
        }

        [Fact]
        public void AutoRegistersSubTypesOnLoad()
        {
            Document<AbstractEntity>().With(x => x.Property);

            using (var session = store.OpenSession())
            {
                session.Store(new MoreDerivedEntity1 { Id = "A", Property = "Asger" });
                session.SaveChanges();
            }

            Reset();

            Document<AbstractEntity>().With(x => x.Property);

            using (var session = store.OpenSession())
            {
                session.Load<AbstractEntity>("A").ShouldBeOfType<MoreDerivedEntity1>();

                configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldNotBe(null);
            }
        }

        [Fact]
        public void AutoRegistersSubTypesOnQuery()
        {
            using (var session = store.OpenSession())
            {
                session.Store(new DerivedEntity { Id = "A" });
                session.Store(new MoreDerivedEntity1 { Id = "B" });
                session.Store(new OtherEntity { Id = "C" }); // unrelated type
                session.SaveChanges();
            }

            Reset();

            configuration.TryGetDesignFor(typeof(DerivedEntity)).ShouldBe(null);
            configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldBe(null);
            configuration.TryGetDesignFor(typeof(OtherEntity)).ShouldBe(null);

            using (var session = store.OpenSession())
            {
                // filters out results that is not subtype of DerivedEntity
                QueryStats stats;
                session.Query<DerivedEntity>().Statistics(out stats).ToList().Count.ShouldBe(2);
                stats.TotalResults.ShouldBe(3);
            }

            configuration.TryGetDesignFor(typeof(DerivedEntity)).ShouldNotBe(null);
            configuration.TryGetDesignFor(typeof(MoreDerivedEntity1)).ShouldNotBe(null);
            configuration.TryGetDesignFor(typeof(OtherEntity)).ShouldNotBe(null);

            using (var session = store.OpenSession())
            {
                // second time around it should filter by discriminators
                QueryStats stats;
                session.Query<DerivedEntity>().Statistics(out stats).ToList().Count.ShouldBe(2);
                stats.TotalResults.ShouldBe(2);
            }
        }

        [Fact]
        public void FailsIfTypeMapperCantMapToConcreteType()
        {
            UseTypeMapper(new FailingTypeMapper());

            using (var session = store.OpenSession())
            {
                session.Store("key", new DerivedEntity());
                session.SaveChanges();
            }

            Reset();

            using (var session = store.OpenSession())
            {
                Should.Throw<InvalidOperationException>(() => session.Load<AbstractEntity>("key"))
                    .Message.ShouldBe("No concrete type could be mapped from discriminator 'NoShow'.");
            }
        }

        public class FailingTypeMapper : ITypeMapper
        {
            public string ToDiscriminator(Type type)
            {
                return "NoShow";
            }

            public Type ToType(string discriminator)
            {
                return null;
            }
        }
    }
}