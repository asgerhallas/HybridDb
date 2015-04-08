using System.IO;
using System.IO.Compression;

namespace HybridDb.Migrations
{
    public class ZipBackupDecorator : IBackupWriter
    {
        readonly IBackupWriter writer;

        public ZipBackupDecorator(IBackupWriter writer)
        {
            this.writer = writer;
        }

        public void Write(string name, byte[] document)
        {
            using (var original = new MemoryStream(document))
            using (var zip = new GZipStream(original, CompressionMode.Decompress))
            using (var zipdocument = new MemoryStream())
            {
                zip.BaseStream.CopyTo(zipdocument);
                writer.Write(name + ".zip", zipdocument.ToArray());
            }
        }
    }
}