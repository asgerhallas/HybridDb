using System;
using System.Collections.Generic;
using HybridDb.Config;
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

            var design = configuration.GetDesignFor<Entity>();
            Should.Throw<InvalidOperationException>(() => new DocumentMigrator(configuration).DeserializeAndMigrate(design, Guid.NewGuid(), new byte[0], 1))
                .Message.ShouldBe("Document version is ahead of configuration. Document is version 1, but configuration is version 0.");
        }

        [Fact]
        public void AppliesDocumentChangeMigration()
        {
            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] = "Peter"; })));

            var design = configuration.GetDesignFor<Entity>();
            var document = configuration.Serializer.Serialize(new Entity { Property = "Asger" });

            var entity = (Entity)new DocumentMigrator(configuration).DeserializeAndMigrate(design, Guid.NewGuid(), document, 0);

            entity.Property.ShouldBe("Peter");
        }

        [Fact]
        public void DoesNotApplyDocumentChangeMigrationForOtherDocumentTypes()
        {
            Document<Entity>();
            UseMigrations(new InlineMigration(1, new ChangeDocumentAsJObject<OtherEntity>(x => { x["Property"] = "Peter"; })));

            var design = configuration.GetDesignFor<Entity>();
            var document = configuration.Serializer.Serialize(new Entity { Property = "Asger" });

            var entity = (Entity)new DocumentMigrator(configuration).DeserializeAndMigrate(design, Guid.NewGuid(), document, 0);

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

            var entity = (Entity)new DocumentMigrator(configuration).DeserializeAndMigrate(design, Guid.NewGuid(), document, 0);

            entity.Property.ShouldBe("Asger123");
        }

        [Fact]
        public void BacksUpDocumentBeforeMigration()
        {
            Document<AbstractEntity>();
            Document<MoreDerivedEntity1>();
            UseMigrations(
                new InlineMigration(1, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "1"; })),
                new InlineMigration(2, new ChangeDocumentAsJObject<Entity>(x => { x["Property"] += "2"; })));

            var backupWriter = new FakeBackupWriter();
            UseBackupWriter(backupWriter);

            var id = Guid.NewGuid();
            var document = configuration.Serializer.Serialize(new MoreDerivedEntity1 { Property = "Asger" });
            var design = configuration.GetDesignFor<MoreDerivedEntity1>();

            new DocumentMigrator(configuration).DeserializeAndMigrate(design, id, document, 0);

            backupWriter.Files.Count.ShouldBe(1);
            backupWriter.Files.ShouldContainKeyAndValue(string.Format("HybridDb.Tests.HybridDbTests+MoreDerivedEntity1_{0}_0", id), document);
        }

        public class FakeBackupWriter : IBackupWriter
        {
            readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

            public Dictionary<string, byte[]> Files
            {
                get { return files; }
            }

            public void Write(DocumentDesign design, Guid id, int version, byte[] document)
            {
                files.Add(string.Format("{0}_{1}_{2}", design.DocumentType.FullName, id, version), document);
            }
        }
    }
}