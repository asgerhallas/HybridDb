using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class ConfigurationTests
    {
        readonly Configuration configuration;

        public ConfigurationTests()
        {
            configuration = new Configuration();
        }

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
            Should.Throw<ArgumentException>(() => configuration.UseMigrations(new List<Migration> { new InlineMigration(2), new InlineMigration(3) }))
                .Message.ShouldBe("Missing migration for version 1.");
        }

        [Fact]
        public void ThrowsIfMigrationVersionHasHoles()
        {
            Should.Throw<ArgumentException>(() => configuration.UseMigrations(new List<Migration> { new InlineMigration(1), new InlineMigration(3) }))
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

        [Fact]
        public void X()
        {
            configuration.Document<Entity>().With("Id", x => x.String);
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