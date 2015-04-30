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
            using (var output = new MemoryStream())
            {
                using (var zip = new GZipStream(output, CompressionMode.Compress))
                using (var input = new MemoryStream(document))
                {
                    input.CopyTo(zip);
                }

                writer.Write(name + ".zip", output.ToArray());
            }
        }
    }
}