using System;
using System.Threading.Tasks;
using HybridDb.Config;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class DeadlockIssueOnMigrations : HybridDbTests
    {
        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForTempDb()
        {
            // We've fixed this by not calling QuerySchema for TempTables. 
            // To get the error again - as you would if you were to find out why sp_getapplock
            // does not behave as expected - then reinstate the call to QuerySchema.
            
            Parallel.For(0, 10, i =>
            {
                var realstore = DocumentStore.ForTesting(TableMode.GlobalTempTables, c =>
                {
                    c.UseConnectionString(connectionString);

                    c.UseTableNamePrefix(Guid.NewGuid().ToString());

                    c.Document<Entity>()
                        .With(x => x.DateTimeProp)
                        .With(x => x.EnumProp)
                        .With(x => x.Field)
                        .With(x => x.Number)
                        .With(x => x.ProjectedProperty)
                        .With(x => x.Property)
                        .With(x => x.Complex.A);
                    c.Document<EntityB>()
                        .With(x => x.DateTimeProp)
                        .With(x => x.EnumProp)
                        .With(x => x.Field)
                        .With(x => x.Number)
                        .With(x => x.ProjectedProperty)
                        .With(x => x.Property)
                        .With(x => x.Complex.A);
                    c.Document<EntityC>()
                        .With(x => x.DateTimeProp)
                        .With(x => x.EnumProp)
                        .With(x => x.Field)
                        .With(x => x.Number)
                        .With(x => x.ProjectedProperty)
                        .With(x => x.Property)
                        .With(x => x.Complex.A);
                    c.Document<EntityD>()
                        .With(x => x.DateTimeProp)
                        .With(x => x.EnumProp)
                        .With(x => x.Field)
                        .With(x => x.Number)
                        .With(x => x.ProjectedProperty)
                        .With(x => x.Property)
                        .With(x => x.Complex.A);
                });

                realstore.Dispose();
            });
        }

        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForRealTables()
        {
            UseRealTables();

            Parallel.For(0, 10, i =>
            {
                var realstore = DocumentStore.Create(c =>
                {
                    c.UseConnectionString(connectionString);

                    c.Document<Entity>()
                        .With(x => x.DateTimeProp)
                        .With(x => x.EnumProp)
                        .With(x => x.Field)
                        .With(x => x.Number)
                        .With(x => x.ProjectedProperty)
                        .With(x => x.Property)
                        .With(x => x.Complex.A);
                    c.Document<EntityB>()
                        .With(x => x.DateTimeProp)
                        .With(x => x.EnumProp)
                        .With(x => x.Field)
                        .With(x => x.Number)
                        .With(x => x.ProjectedProperty)
                        .With(x => x.Property)
                        .With(x => x.Complex.A);
                    c.Document<EntityC>()
                        .With(x => x.DateTimeProp)
                        .With(x => x.EnumProp)
                        .With(x => x.Field)
                        .With(x => x.Number)
                        .With(x => x.ProjectedProperty)
                        .With(x => x.Property)
                        .With(x => x.Complex.A);
                    c.Document<EntityD>()
                        .With(x => x.DateTimeProp)
                        .With(x => x.EnumProp)
                        .With(x => x.Field)
                        .With(x => x.Number)
                        .With(x => x.ProjectedProperty)
                        .With(x => x.Property)
                        .With(x => x.Complex.A);
                });

                realstore.Dispose();
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