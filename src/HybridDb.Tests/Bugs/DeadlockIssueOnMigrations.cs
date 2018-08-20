using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using System.Transactions;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class QueryWithIdTypeMismatch : HybridDbTests
    {
        [Fact(Skip="Skipped until chnage col type migration is done")]
        public void FactMethodName()
        {
            Execute(new CreateTable(new Table("Entities", new Column("Id", typeof (Guid), isPrimaryKey: true))));

            store.Configuration.Document<EntityWithGuidKey>("Entities");
            store.Initialize();

            var session = store.OpenSession();
            session.Store(new EntityWithGuidKey
            {
                Id = Guid.NewGuid()
            });
            session.SaveChanges();
            session.Advanced.Clear();

            session.Query<EntityWithGuidKey>().ToList().Count.ShouldBe(1);
        }

        public class EntityWithGuidKey
        {
            public Guid Id { get; set; }
        }

    }

    public class DeadlockIssueOnMigrations : HybridDbTests
    {
        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForTempDb()
        {
            Parallel.For(0, 10, i =>
            {
                var realstore = Using(DocumentStore.ForTesting(connectionString));

                realstore.Configuration.UseTableNamePrefix(Guid.NewGuid().ToString());

                realstore.Configuration.Document<Entity>()
                    .With(x => x.DateTimeProp)
                    .With(x => x.EnumProp)
                    .With(x => x.Field)
                    .With(x => x.Number)
                    .With(x => x.ProjectedProperty)
                    .With(x => x.Property)
                    .With(x => x.Complex.A);
                realstore.Configuration.Document<EntityB>()
                    .With(x => x.DateTimeProp)
                    .With(x => x.EnumProp)
                    .With(x => x.Field)
                    .With(x => x.Number)
                    .With(x => x.ProjectedProperty)
                    .With(x => x.Property)
                    .With(x => x.Complex.A);
                realstore.Configuration.Document<EntityC>()
                    .With(x => x.DateTimeProp)
                    .With(x => x.EnumProp)
                    .With(x => x.Field)
                    .With(x => x.Number)
                    .With(x => x.ProjectedProperty)
                    .With(x => x.Property)
                    .With(x => x.Complex.A);
                realstore.Configuration.Document<EntityD>()
                    .With(x => x.DateTimeProp)
                    .With(x => x.EnumProp)
                    .With(x => x.Field)
                    .With(x => x.Number)
                    .With(x => x.ProjectedProperty)
                    .With(x => x.Property)
                    .With(x => x.Complex.A);

                realstore.Initialize();
            });
        }

        public class EntityB : ISomeInterface
        {
            public string Id { get; set; }
            public string ProjectedProperty { get; set; }
            public string Field;
            public string Property { get; set; }
            public int Number { get; set; }
            public DateTime DateTimeProp { get; set; }
            public SomeFreakingEnum EnumProp { get; set; }
            public ComplexType Complex { get; set; }

            public class ComplexType
            {
                public string A { get; set; }
                public int B { get; set; }

                public override string ToString()
                {
                    return A + B;
                }
            }
        }

        public class EntityC : ISomeInterface
        {
            public string Id { get; set; }
            public string ProjectedProperty { get; set; }
            public string Field;
            public string Property { get; set; }
            public int Number { get; set; }
            public DateTime DateTimeProp { get; set; }
            public SomeFreakingEnum EnumProp { get; set; }
            public ComplexType Complex { get; set; }

            public class ComplexType
            {
                public string A { get; set; }
                public int B { get; set; }

                public override string ToString()
                {
                    return A + B;
                }
            }
        }

        public class EntityD : ISomeInterface
        {
            public string Id { get; set; }
            public string ProjectedProperty { get; set; }
            public string Field;
            public string Property { get; set; }
            public int Number { get; set; }
            public DateTime DateTimeProp { get; set; }
            public SomeFreakingEnum EnumProp { get; set; }
            public ComplexType Complex { get; set; }

            public class ComplexType
            {
                public string A { get; set; }
                public int B { get; set; }

                public override string ToString()
                {
                    return A + B;
                }
            }
        }

    }
}