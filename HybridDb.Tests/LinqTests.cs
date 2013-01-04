using System;
using Xunit;
using System.Linq;
using Shouldly;

namespace HybridDb.Tests
{
    public class LinqTests
    {
        readonly string connectionString;
        readonly DocumentStore store;

        public LinqTests()
        {
            connectionString = "data source=.;Integrated Security=True";
            store = new DocumentStore(connectionString);
            store.ForDocument<Entity>()
                 .Projection(x => x.Property)
                 .Projection(x => x.StringProp)
                 .Projection(x => x.TheChild.NestedProperty);
            store.Configuration.UseSerializer(new DefaultJsonSerializer());
            store.Initialize();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Id = Guid.NewGuid(), Property = 1, StringProp = "Asger"});
                session.Store(new Entity { Id = Guid.NewGuid(), Property = 2, StringProp = "Lars"});
                session.Store(new Entity { Id = Guid.NewGuid(), Property = 3, StringProp = null});
                session.SaveChanges();
            }
        }

        public void Dispose()
        {
            store.Dispose();
        }

        [Fact]
        public void CanQueryAll()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().ToList();
                result.Count.ShouldBe(3);
            }
        }

        [Fact]
        public void CanQueryWithWhereEquals()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property == 2).ToList();
                result.Single().Property.ShouldBe(2);
            }
        }

        [Fact]
        public void CanQueryWithWhereGreaterThan()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property > 1).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 2);
                result.ShouldContain(x => x.Property == 3);
            }
        }

        [Fact]
        public void CanQueryWithWhereGreaterThanOrEqual()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property >= 2).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 2);
                result.ShouldContain(x => x.Property == 3);
            }
        }

        [Fact]
        public void CanQueryWithWhereLessThanOrEqual()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property <= 2).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 1);
                result.ShouldContain(x => x.Property == 2);
            }
        }

        [Fact]
        public void CanQueryWithWhereLessThan()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property < 2).ToList();
                result.Count.ShouldBe(1);
                result.ShouldContain(x => x.Property == 1);
            }
        }

        [Fact]
        public void CanQueryWithWhereNotEquals()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.Property != 2).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 1);
                result.ShouldContain(x => x.Property == 3);
            }
        }

        [Fact]
        public void CanQueryWithWhereNull()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.StringProp == null).ToList();
                result.Count.ShouldBe(1);
                result.ShouldContain(x => x.Property == 3);
            }
        }

        [Fact]
        public void CanQueryWithWhereNotNull()
        {
            using (var session = store.OpenSession())
            {
                var result = session.Query<Entity>().Where(x => x.StringProp != null).ToList();
                result.Count.ShouldBe(2);
                result.ShouldContain(x => x.Property == 1);
                result.ShouldContain(x => x.Property == 2);
            }
        }

        public class Entity
        {
            public Entity()
            {
                TheChild = new Child();
            }

            public string Field;
            public Guid Id { get; set; }
            public int Property { get; set; }
            public string StringProp { get; set; }
            public DateTime DateTimeProp { get; set; }
            public Child TheChild { get; set; }

            public class Child
            {
                public double NestedProperty { get; set; }
            }
        }
    }
}