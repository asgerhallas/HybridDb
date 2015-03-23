using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HybridDb.Config;
using HybridDb.Migrations;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class ConfigurationTests
    {
        private readonly Configuration configuration;

        public ConfigurationTests()
        {
            configuration = new Configuration();
        }

        public Dictionary<string, Projection> ProjectionsFor<T>()
        {
            return configuration.GetDesignFor<T>().Projections;
        }

        public DocumentTable TableFor<T>()
        {
            return configuration.GetDesignFor<T>().Table;
        }

        [Fact]
        public void CanGetColumnNameFromSimpleProjection()
        {
            configuration.Document<Entity>().With(x => x.String);
            ProjectionsFor<Entity>().ShouldContainKey("String");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithMethod()
        {
            configuration.Document<Entity>().With(x => x.String.ToUpper());
            ProjectionsFor<Entity>().ShouldContainKey("StringToUpper");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithMethodAndArgument()
        {
            configuration.Document<Entity>().With(x => x.String.ToUpper(CultureInfo.InvariantCulture));
            ProjectionsFor<Entity>().ShouldContainKey("StringToUpperCultureInfoInvariantCulture");
        }

        [Fact(Skip = "until we support multikey indices")]
        public void CanGetColumnNameFromProjectionWithLambda()
        {
            configuration.Document<Entity>().With(x => x.Strings.Where(y => y == "Asger"));
            ProjectionsFor<Entity>().ShouldContainKey("StringsWhereEqualAsger");
        }

        [Fact(Skip = "until we support multikey indices")]
        public void CanGetColumnNameFromProjectionWithComplexLambda()
        {
            configuration.Document<Entity>().With(x => x.Strings.Where(y => y.PadLeft(2).Length > 10));
            ProjectionsFor<Entity>().ShouldContainKey("StringsWherePadLeft2LengthGreaterThan10");
        }

        [Fact]
        public void CanGetColumnNameFromProjectionWithEnumFlags()
        {
            configuration.Document<Entity>().With(x => x.String.GetType().GetProperties(BindingFlags.Static | BindingFlags.Instance).Any());
            ProjectionsFor<Entity>().ShouldContainKey("StringGetTypeGetPropertiesInstanceStaticAny");
        }

        [Fact]
        public void CanOverrideProjectionsForSubtype()
        {
            configuration.Document<AbstractEntity>()
                .With("Number", x => 1);

            configuration.Document<DerivedEntity>()
                .With("Number", x => 2);

            ProjectionsFor<DerivedEntity>()["Number"].Projector(new DerivedEntity()).ShouldBe(2);
        }

        [Fact]
        public void ProjectionDirectlyFromEntity()
        {
            configuration.Document<Entity>().With(x => x.String);

            ProjectionsFor<Entity>()["String"].Projector(new Entity { String = "Asger" }).ShouldBe("Asger");
        }

        [Fact]
        public void ProjectionDirectlyFromEntityWithOtherClassAsExtension()
        {
            configuration.Document<OtherEntity>()
                .With(x => x.String)
                .Extend<Index>(e => 
                    e.With(x => x.Number, x => x.String.Length));

            ProjectionsFor<OtherEntity>()["String"].Projector(new OtherEntity { String = "Asger" }).ShouldBe("Asger");
            ProjectionsFor<OtherEntity>()["Number"].Projector(new OtherEntity { String = "Asger" }).ShouldBe(5);
        }
        
        [Fact]
        public void LastProjectionOfSameNameWins()
        {
            configuration.Document<OtherEntity>()
                .With(x => x.String)
                .Extend<Index>(e =>
                    e.With(x => x.String, x => x.String.Replace("a", "b")));

            ProjectionsFor<OtherEntity>()["String"].Projector(new OtherEntity { String = "asger" }).ShouldBe("bsger");
        }
        
        [Fact]
        public void AddsNonNullableColumnForNonNullableProjection()
        {
            configuration.Document<AbstractEntity>().With(x => x.Number);

            var sqlColumn = TableFor<AbstractEntity>()["Number"];
            sqlColumn.Type.ShouldBe(typeof(int));
            sqlColumn.Nullable.ShouldBe(false);
        }

        [Fact]
        public void AddNullableColumnForProjectionOnSubtypes()
        {
            configuration.Document<AbstractEntity>();
            configuration.Document<MoreDerivedEntity1>().With(x => x.Number);
            configuration.Document<MoreDerivedEntity2>();

            var sqlColumn = TableFor<AbstractEntity>()["Number"];
            sqlColumn.Type.ShouldBe(typeof(int));
            sqlColumn.Nullable.ShouldBe(true);
        }

        [Fact]
        public void FailsWhenTryingToOverrideProjectionWithNonCompatibleType()
        {
            configuration.Document<AbstractEntity>().With(x => x.Number);

            Should.Throw<InvalidOperationException>(() => configuration.Document<MoreDerivedEntity1>().With("Number", x => "OtherTypeThanBase"))
                .Message.ShouldBe("Can not override projection for Number of type System.Int32 with a projection that returns System.String (on HybridDb.Tests.ConfigurationTests+MoreDerivedEntity1).");
        }

        [Fact]
        public void FailsWhenOverridingProjectionOnSiblingWithNonCompatibleType()
        {
            configuration.Document<AbstractEntity>();
            configuration.Document<MoreDerivedEntity1>().With("Number", x => 1);

            Should.Throw<InvalidOperationException>(() => configuration.Document<MoreDerivedEntity2>().With("Number", x => "OtherTypeThanBase"))
                .Message.ShouldBe("Can not override projection for Number of type System.Int32 with a projection that returns System.String (on HybridDb.Tests.ConfigurationTests+MoreDerivedEntity2).");
        }

        [Fact]
        public void FailsWhenOverridingProjectionOnSelfWithNonCompatibleType()
        {
            Should.Throw<InvalidOperationException>(() =>
                configuration.Document<OtherEntity>()
                    .With("String", x => 1)
                    .With("String", x => "string"));
        }

        [Fact]
        public void CanOverrideProjectionWithCompatibleType()
        {
            configuration.Document<AbstractEntity>().With(x => x.LongNumber);
            configuration.Document<MoreDerivedEntity1>().With("LongNumber", x => x.Number);

            var sqlColumn = TableFor<AbstractEntity>()["LongNumber"];
            sqlColumn.Type.ShouldBe(typeof(long));
            sqlColumn.Nullable.ShouldBe(false);

            ProjectionsFor<AbstractEntity>()["LongNumber"].Projector(new MoreDerivedEntity1 { LongNumber = 1, Number = 2 }).ShouldBe(1);
            ProjectionsFor<MoreDerivedEntity1>()["LongNumber"].Projector(new MoreDerivedEntity1 { LongNumber = 1, Number = 2 }).ShouldBe(2);
        }

        [Fact]
        public void CanOverrideProjectionWithNullability()
        {
            configuration.Document<AbstractEntity>().With(x => x.Number);
            configuration.Document<MoreDerivedEntity1>().With("Number", x => (int?)null);

            var sqlColumn = TableFor<AbstractEntity>()["Number"];
            sqlColumn.Type.ShouldBe(typeof(int));
            sqlColumn.Nullable.ShouldBe(true);
        }

        [Fact]
        public void CanOverrideProjectionWithoutChangingNullability()
        {
            configuration.Document<AbstractEntity>().With(x => x.Number);
            configuration.Document<MoreDerivedEntity1>().With(x => x.Number);

            var sqlColumn = TableFor<AbstractEntity>()["Number"];
            sqlColumn.Type.ShouldBe(typeof(int));
            sqlColumn.Nullable.ShouldBe(false);
        }

        [Fact]
        public void FailsWhenRegisteringSubtypeBeforeBase()
        {
            configuration.Document<MoreDerivedEntity1>();
            Should.Throw<InvalidOperationException>(() => configuration.Document<AbstractEntity>())
                .Message.ShouldBe("Document HybridDb.Tests.ConfigurationTests+AbstractEntity must be configured before its subtype HybridDb.Tests.ConfigurationTests+MoreDerivedEntity1.");
        }
        
        [Fact]
        public void SetDiscriminator()
        {
            configuration.Document<AbstractEntity>(discriminator: "abe");
            configuration.Document<MoreDerivedEntity1>(discriminator: "gris");
            configuration.Document<MoreDerivedEntity2>();

            configuration.GetDesignFor<AbstractEntity>().Discriminator.ShouldBe("abe");
            configuration.GetDesignFor<MoreDerivedEntity1>().Discriminator.ShouldBe("gris");
            configuration.GetDesignFor<MoreDerivedEntity2>().Discriminator.ShouldBe("MoreDerivedEntity2");
        }

        [Fact]
        public void FailOnDuplicateDiscriminators()
        {
            configuration.Document<AbstractEntity>(discriminator: "abe");
            Should.Throw<InvalidOperationException>(() => 
                configuration.Document<MoreDerivedEntity1>(discriminator: "abe"))
                .Message.ShouldBe("Discriminator 'abe' is already in use.");
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

        public class Entity
        {
            public string String { get; set; }
            public List<string> Strings { get; set; }
            public int Number { get; set; }
        }

        public class OtherEntity
        {
            public string String { get; set; }
        }

        public abstract class AbstractEntity
        {
            public Guid Id { get; set; }
            public string Property { get; set; }
            public int Number { get; set; }
            public long LongNumber { get; set; }
        }

        public class DerivedEntity : AbstractEntity { }
        public class MoreDerivedEntity1 : DerivedEntity { }
        public class MoreDerivedEntity2 : DerivedEntity { }

        public class Index
        {
            public string String { get; set; }
            public int Number { get; set; }
        }

        public class IndexWithId
        {
            public Guid Id { get; set; }
            public string String { get; set; }
            public int Number { get; set; }
        }

        public class OtherIndex
        {
            public string String { get; set; }
            public int Number { get; set; }
        }

        public class WrongMemberTypeIndex
        {
            public int String;
        }

        public class WrongPropertyTypeIndex
        {
            public int String { get; set; }
            public int? Number { get; set; }
        }

        public class BadNameMatchIndex
        {
            public int BadMatch { get; set; }
        }
    }
}