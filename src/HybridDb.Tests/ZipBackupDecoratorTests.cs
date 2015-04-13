using System.IO;
using System.IO.Compression;
using HybridDb.Migrations;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class ZipBackupDecoratorTests
    {
        [Fact]
        public void RewritesAsZip()
        {
            var inner = new FakeBackupWriter();
            var decorator = new ZipBackupDecorator(inner);

            var document = new byte[] { 65, 66, 67 };

            decorator.Write("jacob.bak", document);

            using (var input = new MemoryStream(inner.Files["jacob.bak.zip"]))
            using (var zip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                zip.CopyTo(output);

                output.ToArray().ShouldBe(document);
            }
        }

        [Fact]
        public void WriteToZipFile()
        {
            var inner = new FileBackupWriter(".");
            var decorator = new ZipBackupDecorator(inner);

            var document = new byte[] { 65, 66, 67 };

            decorator.Write("jacob.bak", document);
        }
    }
}