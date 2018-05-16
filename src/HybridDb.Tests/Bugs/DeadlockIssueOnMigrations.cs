using System;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
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
            new CreateTable(new Table("Entities", new Column("Id", typeof (Guid), isPrimaryKey: true))).Execute(store.Database);

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
        public void ShouldTryNotToDeadlockOnSchemaMigationsForTempTables()
        {
            // We've fixed this by not calling QuerySchema for TempTables. 
            // To get the error again - as you would if you were to find out why sp_getapplock
            // does not behave as expected - then reinstate the call to QuerySchema.

            Parallel.For(0, 100, i =>
            {
                var realstore = DocumentStore.ForTesting(TableMode.UseTempTables, connectionString);

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

                realstore.Dispose();
            });
        }

        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForTempDb()
        {
            Parallel.For(0, 100, i =>
            {
                var realstore = DocumentStore.ForTesting(TableMode.UseTempDb, connectionString);

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

                realstore.Dispose();
            });
        }

        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForRealTables()
        {
            UseRealTables();

            Parallel.For(0, 100, i =>
            {
                var realstore = Using(new DocumentStore(new Configuration(), TableMode.UseRealTables, connectionString, true));
                realstore.Configuration.Document<Entity>();
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