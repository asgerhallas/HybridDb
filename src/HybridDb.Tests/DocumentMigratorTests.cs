using System;
using HybridDb.Migrations;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentMigratorTests : HybridDbStoreTests
    {
        [Fact]
        public void ThrowsIfADocumentIsNewerThanExpected()
        {
            Document<Entity>();

            var id = NewId();
            var design = configuration.GetDesignFor<Entity>();
            Should.Throw<InvalidOperationException>(() => new DocumentMigrator(configuration).DeserializeAndMigrate(design, id, new byte[0], 1))
                .Message.ShouldBe(string.Format("Document HybridDb.Tests.HybridDbTests+Entity/{0} version is ahead of configuration. Document is version 1, but configuration is version 0.", id));
        }

        [Fact]
        public void AppliesDocumentChangeMigration()
        {
            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] = "Peter"; })));

            var design = configuration.GetDesignFor<Entity>();
            var document = configuration.Serializer.Serialize(new Entity { Property = "Asger" });

            var entity = (Entity)new DocumentMigrator(configuration).DeserializeAndMigrate(design, NewId(), document, 0);

            entity.Property.ShouldBe("Peter");
        }

        [Fact]
        public void DoesNotApplyDocumentChangeMigrationForOtherDocumentTypes()
        {
            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<OtherEntity>(x => { x["Property"] = "Peter"; })));

            var design = configuration.GetDesignFor<Entity>();
            var document = configuration.Serializer.Serialize(new Entity { Property = "Asger" });

            var entity = (Entity)new DocumentMigrator(configuration).DeserializeAndMigrate(design, NewId(), document, 0);

            entity.Property.ShouldBe("Asger");
        }

        [Fact]
        public void AppliesMigrationsInOrder()
        {
            Document<Entity>();
            UseMigrations(
                new InlineMigration(2, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "2"; })),
                new InlineMigration(3, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "3"; })),
                new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "1"; })));

            var design = configuration.GetDesignFor<Entity>();
            var document = configuration.Serializer.Serialize(new Entity { Property = "Asger" });

            var entity = (Entity)new DocumentMigrator(configuration).DeserializeAndMigrate(design, NewId(), document, 0);

            entity.Property.ShouldBe("Asger123");
        }

    }
}