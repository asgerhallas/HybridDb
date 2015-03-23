using System;
using System.IO;
using HybridDb.Migrations;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class FileBackupWriterTests : HybridDbTests
    {
        [Fact]
        public void WritesToFile()
        {
            Document<Entity>();

            var id = Guid.NewGuid();

            var writer = new FileBackupWriter(".");
            writer.Write(configuration.GetDesignFor<Entity>(), id, 5, new byte[]{ 1, 2, 3 });

            var bytes = File.ReadAllBytes(string.Format("HybridDb.Tests.HybridDbTests+Entity_{0}_5.bak", id));
            bytes.ShouldBe(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void CanWriteSameDocumentTwice()
        {
            Document<Entity>();

            var id = Guid.NewGuid();

            var writer = new FileBackupWriter(".");
            writer.Write(configuration.GetDesignFor<Entity>(), id, 5, new byte[]{ 1, 2, 3 });
            
            Should.NotThrow(() => writer.Write(configuration.GetDesignFor<Entity>(), id, 5, new byte[]{ 1, 2, 3 }));

            var bytes = File.ReadAllBytes(string.Format("HybridDb.Tests.HybridDbTests+Entity_{0}_5.bak", id));
            bytes.ShouldBe(new byte[] { 1, 2, 3 });
        }
    }
}