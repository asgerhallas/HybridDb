using System.IO;
using System.Runtime.InteropServices;

namespace HybridDb.Migrations.Documents
{
    public class FileBackupWriter : IBackupWriter
    {
        readonly string path;

        public FileBackupWriter(string path)
        {
            this.path = path;

            Directory.CreateDirectory(path);
        }

        public void Write(string name, byte[] document)
        {
            try
            {
                File.WriteAllBytes(Path.Combine(path, name), document);
            }
            catch (IOException e)
            {
                var errorCode = Marshal.GetHRForException(e) & ((1 << 16) - 1);
                if (errorCode != 32 && errorCode != 33) throw;
            }
        }
    }
}