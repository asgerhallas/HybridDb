using System;
using System.IO;
using HybridDb.Config;

namespace HybridDb.Migrations
{
    public class FileBackupWriter : IBackupWriter
    {
        readonly string path;

        public FileBackupWriter(string path)
        {
            this.path = path;

            Directory.CreateDirectory(path);
        }

        public void Write(DocumentDesign design, Guid id, int version, byte[] document)
        {
            File.WriteAllBytes(Path.Combine(path, string.Format("{0}_{1}_{2}.bak", design.DocumentType.FullName, id, version)), document);
        }
    }
}