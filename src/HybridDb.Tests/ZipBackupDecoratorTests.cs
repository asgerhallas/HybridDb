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

            var document = new byte[] { 1, 2, 3 };

            decorator.Write("jacob.bak", document);

            using (var s = new MemoryStream(inner.Files["jacob.bak.zip"]))
            using (var z = new GZipStream(s, CompressionMode.Decompress))
            using (var r = new MemoryStream())
            {
                z.BaseStream.CopyTo(r);

                r.ToArray().ShouldBe(document);
            }
        }
    }
}