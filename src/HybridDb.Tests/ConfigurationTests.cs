using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Documents;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class ConcurrencyTests
    {
        readonly ITestOutputHelper output;

        public ConcurrencyTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        //[Fact]
        //public void Babs()
        //{
        //    var enumerable = Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
        //    {
        //        var documentStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, c =>
        //        {
        //            c.DisableBackgroundMigrations();
        //            c.Document<A>()
        //                .With(x => x.AA)
        //                .With(x => x.BB)
        //                .With(x => x.CC)
        //                .With(x => x.DD)
        //                .With(x => x.EE)
        //                .With(x => x.FF)
        //                .With(x => x.GG)
        //                .With(x => x.HH);

        //            c.Document<B>()
        //                .With(x => x.AA)
        //                .With(x => x.BB)
        //                .With(x => x.CC)
        //                .With(x => x.DD)
        //                .With(x => x.EE)
        //                .With(x => x.FF)
        //                .With(x => x.GG)
        //                .With(x => x.HH);

        //            c.Document<C>()
        //                .With(x => x.AA)
        //                .With(x => x.BB)
        //                .With(x => x.CC)
        //                .With(x => x.DD)
        //                .With(x => x.EE)
        //                .With(x => x.FF)
        //                .With(x => x.GG)
        //                .With(x => x.HH);

        //            c.Document<D>()
        //                .With(x => x.AA)
        //                .With(x => x.BB)
        //                .With(x => x.CC)
        //                .With(x => x.DD)
        //                .With(x => x.EE)
        //                .With(x => x.FF)
        //                .With(x => x.GG)
        //                .With(x => x.HH);

        //            c.Document<E>()
        //                .With(x => x.AA)
        //                .With(x => x.BB)
        //                .With(x => x.CC)
        //                .With(x => x.DD)
        //                .With(x => x.EE)
        //                .With(x => x.FF)
        //                .With(x => x.GG)
        //                .With(x => x.HH);
        //        });

        //        var documentSession = documentStore.OpenSession();
        //        documentSession.Store("a", new E());
        //        documentSession.Store("B", new D());
        //        documentSession.Store("c", new C());
        //        documentSession.Store("d", new B());
        //        documentSession.SaveChanges();
        //        documentSession.Advanced.Clear();
        //        documentSession.Load<E>("a");
        //        documentSession.Load<D>("b");
        //        documentSession.Load<C>("c");
        //        documentSession.Load<B>("d");

        //        documentStore.Dispose();
        //    }));

        //    Task.WaitAll(enumerable.ToArray());
        //}

        public class A
        {
            public string AA { get; set; }
            public string BB { get; set; }
            public string CC { get; set; }
            public string DD { get; set; }
            public string EE { get; set; }
            public string FF { get; set; }
            public string GG { get; set; }
            public string HH { get; set; }
        }

        public class B
        {
            public string AA { get; set; }
            public string BB { get; set; }
            public string CC { get; set; }
            public string DD { get; set; }
            public string EE { get; set; }
            public string FF { get; set; }
            public string GG { get; set; }
            public string HH { get; set; }
        }

        public class C
        {
            public string AA { get; set; }
            public string BB { get; set; }
            public string CC { get; set; }
            public string DD { get; set; }
            public string EE { get; set; }
            public string FF { get; set; }
            public string GG { get; set; }
            public string HH { get; set; }
        }

        public class D
        {
            public string AA { get; set; }
            public string BB { get; set; }
            public string CC { get; set; }
            public string DD { get; set; }
            public string EE { get; set; }
            public string FF { get; set; }
            public string GG { get; set; }
            public string HH { get; set; }
        }

        public class E
        {
            public string AA { get; set; }
            public string BB { get; set; }
            public string CC { get; set; }
            public string DD { get; set; }
            public string EE { get; set; }
            public string FF { get; set; }
            public string GG { get; set; }
            public string HH { get; set; }
        }

        public class F
        {
            public string AA { get; set; }
            public string BB { get; set; }
            public string CC { get; set; }
            public string DD { get; set; }
            public string EE { get; set; }
            public string FF { get; set; }
            public string GG { get; set; }
            public string HH { get; set; }
        }

        public class G
        {
            public string AA { get; set; }
            public string BB { get; set; }
            public string CC { get; set; }
            public string DD { get; set; }
            public string EE { get; set; }
            public string FF { get; set; }
            public string GG { get; set; }
            public string HH { get; set; }
        }

        public class H
        {
            public string AA { get; set; }
            public string BB { get; set; }
            public string CC { get; set; }
            public string DD { get; set; }
            public string EE { get; set; }
            public string FF { get; set; }
            public string GG { get; set; }
            public string HH { get; set; }
        }

        public class I
        {
            public string AA { get; set; }
            public string BB { get; set; }
            public string CC { get; set; }
            public string DD { get; set; }
            public string EE { get; set; }
            public string FF { get; set; }
            public string GG { get; set; }
            public string HH { get; set; }
        }

    }


    public class ConfigurationTests
    {
        readonly Configuration configuration;

        public ConfigurationTests() => configuration = new Configuration();

        [Fact]
        public void RegisterDocumentDesign()
        {
            configuration.Document<Entity>();

            var design = configuration.DocumentDesigns.Single();
            design.DocumentType.ShouldBe(typeof(Entity));
            design.Discriminator.ShouldBe(typeof(Entity).AssemblyQualifiedName);
            design.Parent.ShouldBe(null);
            design.DecendentsAndSelf.Values.ShouldBe(new[] { design });

            var table = configuration.Tables.Single();
            table.Key.ShouldBe("Entities");
        }

        [Fact]
        public void RegisterDerivedTypeAsDocument()
        {
            configuration.Document<AbstractEntity>();
            configuration.Document<DerivedEntity>();

            var designs = configuration.DocumentDesigns;
            designs[0].DocumentType.ShouldBe(typeof(AbstractEntity));
            designs[0].Parent.ShouldBe(null);
            designs[0].DecendentsAndSelf.Values.ShouldBe(new[] { designs[0], designs[1] });

            designs[1].DocumentType.ShouldBe(typeof(DerivedEntity));
            designs[1].Parent.ShouldBe(designs[0]);
            designs[1].DecendentsAndSelf.Values.ShouldBe(new[] { designs[1] });

            var table = configuration.Tables.Single();
            table.Key.ShouldBe("AbstractEntities");
        }

        [Fact]
        public void RegisterDerivedTypeAsDocument_WithExplicitTableName()
        {
            configuration.Document<AbstractEntity>("mytable");
            configuration.Document<DerivedEntity>("mytable");

            var designs = configuration.DocumentDesigns;
            designs[0].DocumentType.ShouldBe(typeof(AbstractEntity));
            designs[0].Parent.ShouldBe(null);
            designs[0].DecendentsAndSelf.Values.ShouldBe(new[] { designs[0], designs[1] });

            designs[1].DocumentType.ShouldBe(typeof(DerivedEntity));
            designs[1].Parent.ShouldBe(designs[0]);
            designs[1].DecendentsAndSelf.Values.ShouldBe(new[] { designs[1] });

            var table = configuration.Tables.Single();
            table.Key.ShouldBe("mytable");
        }

        [Fact]
        public void RegisterBaseTypeAfterDerivedTypeBeforeInitialize()
        {
            configuration.Document<MoreDerivedEntity1>();

            Should.Throw<InvalidOperationException>(() => configuration.Document<DerivedEntity>());
        }

        [Fact]
        public void RegisterBaseTypeAfterDerivedTypeAfterInitialize()
        {
            configuration.Document<MoreDerivedEntity1>();
            configuration.Initialize();
            configuration.Document<DerivedEntity>();

            var designs = configuration.DocumentDesigns;
            designs[0].DocumentType.ShouldBe(typeof(object));
            designs[0].Parent.ShouldBe(null);
            designs[0].DecendentsAndSelf.Values.ShouldBe(new[] { designs[0], designs[1] });

            designs[1].DocumentType.ShouldBe(typeof(DerivedEntity));
            designs[1].Parent.ShouldBe(designs[0]);
            designs[1].DecendentsAndSelf.Values.ShouldBe(new[] { designs[1] });

            designs[2].DocumentType.ShouldBe(typeof(MoreDerivedEntity1));
            designs[2].Parent.ShouldBe(null);
            designs[2].DecendentsAndSelf.Values.ShouldBe(new[] { designs[2] });

            var tables = configuration.Tables.Keys.OrderBy(x => x).ToList();
            tables.Count.ShouldBe(2);
            tables[0].ShouldBe("Documents");
            tables[1].ShouldBe("MoreDerivedEntity1s");
        }

        [Fact]
        public void RegisterDerivedDocumentToOtherTable()
        {
            configuration.Document<AbstractEntity>();
            configuration.Document<DerivedEntity>("othertable");

            var designs = configuration.DocumentDesigns;
            designs[0].DocumentType.ShouldBe(typeof(AbstractEntity));
            designs[0].Parent.ShouldBe(null);
            designs[0].DecendentsAndSelf.Values.ShouldBe(new[] { designs[0] });

            designs[1].DocumentType.ShouldBe(typeof(DerivedEntity));
            designs[1].Parent.ShouldBe(null);
            designs[1].DecendentsAndSelf.Values.ShouldBe(new[] { designs[1] });

            var tables = configuration.Tables.Keys.OrderBy(x => x).ToList();
            tables.Count.ShouldBe(2);
            tables[0].ShouldBe("AbstractEntities");
            tables[1].ShouldBe("othertable");
        }

        [Fact]
        public void FailsWhenTryingToAddNewTableAfterInitialize()
        {
            configuration.Initialize(); 

            Should.Throw<InvalidOperationException>(() => configuration.GetOrCreateDesignFor(typeof(OtherEntity), "allnewtable"));
        }

        [Fact]
        public void CanUseCustomTypeMapper()
        {
            configuration.UseTypeMapper(new OtherTypeMapper("MySiscriminator"));

            configuration.Document<Entity>();

            var design = configuration.DocumentDesigns.Single();
            design.Discriminator.ShouldBe("MySiscriminator");
        }

        [Fact]
        public void FailWhenSettingTypeMapperTooLate()
        {
            configuration.Document<Entity>();
            Should.Throw<InvalidOperationException>(() => configuration.UseTypeMapper(new OtherTypeMapper("MySiscriminator")));
        }

        [Fact]
        public void FailOnDuplicateDiscriminators()
        {
            configuration.UseTypeMapper(new OtherTypeMapper("AlwaysTheSame"));

            configuration.Document<AbstractEntity>();

            Should.Throw<InvalidOperationException>(() =>
                configuration.Document<MoreDerivedEntity1>())
                .Message.ShouldBe("Discriminator 'AlwaysTheSame' is already in use.");
        }

        [Fact]
        public void AssignsClosestParentToDocumentDesign()
        {
            configuration.Document<AbstractEntity>();
            configuration.Document<DerivedEntity>();
            configuration.Document<MoreDerivedEntity1>();
            configuration.Document<MoreDerivedEntity2>();

            configuration.GetDesignFor<AbstractEntity>().Parent.ShouldBe(null);
            configuration.GetDesignFor<DerivedEntity>().Parent.ShouldBe(configuration.GetDesignFor<AbstractEntity>());
            configuration.GetDesignFor<MoreDerivedEntity1>().Parent.ShouldBe(configuration.GetDesignFor<DerivedEntity>());
            configuration.GetDesignFor<MoreDerivedEntity2>().Parent.ShouldBe(configuration.GetDesignFor<DerivedEntity>());
        }

        [Fact]
        public void CanReportInitialVersion()
        {
            configuration.ConfiguredVersion.ShouldBe(0);
        }

        [Fact]
        public void CanReportVersion()
        {
            configuration.UseMigrations(new List<Migration> { new InlineMigration(1), new InlineMigration(2) });
            configuration.ConfiguredVersion.ShouldBe(2);
        }

        [Fact]
        public void ThrowsIfMigrationsDoesNotStartFromOne()
        {
            Should.Throw<ArgumentException>(() =>
                {
                    configuration.UseMigrations(new List<Migration> {new InlineMigration(2), new InlineMigration(3)});

                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                    configuration.Migrations.ToList();
                })
                .Message.ShouldBe("Missing migration for version 1.");
        }

        [Fact]
        public void ThrowsIfMigrationVersionHasHoles()
        {
            Should.Throw<ArgumentException>(() =>
                {
                    configuration.UseMigrations(new List<Migration> {new InlineMigration(1), new InlineMigration(3)});
                    
                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                    configuration.Migrations.ToList();
                })
                .Message.ShouldBe("Missing migration for version 2.");
        }

        [Fact]
        public void HasDefaultBackupWriter()
        {
            configuration.BackupWriter.ShouldBeOfType<NullBackupWriter>();
        }

        [Fact]
        public void CanConfigureBackupWriter()
        {
            var writer = new FileBackupWriter("test");
            configuration.UseBackupWriter(writer);
            configuration.BackupWriter.ShouldBe(writer);
        }

        [Fact]
        public void FailWhenTryingtoOverrideIdProjection()
        {
            Should.Throw<ArgumentException>(() => configuration.Document<Entity>().With("Id", x => x.String));
        }

        [Fact]
        public void FailsIfEntityTypeIsUnknown()
        {
            Should.Throw<HybridDbException>(() => configuration.GetDesignFor<int>());
        }

        [Fact]
        public void FailIfDiscriminatorIsTooLong()
        {
            configuration.UseTypeMapper(new OtherTypeMapper(string.Join("", Enumerable.Repeat("A", 1025))));

            Should.Throw<InvalidOperationException>(() => configuration.Document<Entity>());
        }

        public class OtherTypeMapper : ITypeMapper
        {
            readonly string discriminator;

            public OtherTypeMapper(string discriminator)
            {
                this.discriminator = discriminator;
            }

            public string ToDiscriminator(Type type)
            {
                return discriminator;
            }

            public Type ToType(string discriminator)
            {
                throw new NotImplementedException();
            }
        }

        class Entity
        {
            public string String { get; set; }
        }

        class OtherEntity
        {
        }

        abstract class AbstractEntity
        {
            public Guid Id { get; set; }
            public string Property { get; set; }
            public int Number { get; set; }
            public long LongNumber { get; set; }
        }

        class DerivedEntity : AbstractEntity { }
        class MoreDerivedEntity1 : DerivedEntity { }
        class MoreDerivedEntity2 : DerivedEntity { }
    }
}