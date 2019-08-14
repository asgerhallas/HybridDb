using System;
using System.Collections.Generic;
using HybridDb.Migrations.Documents;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentMigratorTests : HybridDbTests
    {
        [Fact]
        public void AppliesDocumentChangeMigration()
        {
            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] = "Peter"; })));

            var design = configuration.GetDesignFor<Entity>();
            var document = configuration.Serializer.Serialize(new Entity { Property = "Asger" });

            var id = NewId();

            var entity = (Entity)new DocumentMigrator(configuration).DeserializeAndMigrate(null, design, Row(id, document, 0, design.Discriminator));

            entity.Property.ShouldBe("Peter");
        }

        [Fact]
        public void ThrowsIfADocumentIsNewerThanExpected()
        {
            Document<Entity>();

            var id = NewId();
            var design = configuration.GetDesignFor<Entity>();

            Should.Throw<InvalidOperationException>(() => new DocumentMigrator(configuration).DeserializeAndMigrate(null, design, Row(id, "", 1, design.Discriminator)))
                .Message.ShouldBe($"Document HybridDb.Tests.HybridDbTests+Entity/{id} version is ahead of configuration. Document is version 1, but configuration is version 0.");
        }

        [Fact]
        public void DoesNotApplyDocumentChangeMigrationForOtherDocumentTypes()
        {
            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<OtherEntity>(x => { x["Property"] = "Peter"; })));

            var design = configuration.GetDesignFor<Entity>();
            var document = configuration.Serializer.Serialize(new Entity { Property = "Asger" });

            var id = NewId();
            var entity = (Entity)new DocumentMigrator(configuration).DeserializeAndMigrate(null, design, Row(id, document, 0, design.Discriminator));

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
            var document = configuration.Serializer.Serialize(new Entity {Property = "Asger"});

            var id = NewId();
            var entity = (Entity) new DocumentMigrator(configuration).DeserializeAndMigrate(null, design, Row(id, document, 0, design.Discriminator));

            entity.Property.ShouldBe("Asger123");
        }

        static IDictionary<string, object> Row(string id, string document, int version, string discriminator) =>
            new Dictionary<string, object>
            {
                ["Id"] = id,
                ["Document"] = document,
                ["Version"] = version,
                ["Discriminator"] = discriminator
            };
    }
}