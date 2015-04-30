using System.Collections.Generic;
using HybridDb.Migrations;

namespace HybridDb.Tests
{
    public class FakeBackupWriter : IBackupWriter
    {
        readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

        public Dictionary<string, byte[]> Files
        {
            get { return files; }
        }

        public void Write(string name, byte[] document)
        {
            files.Add(name, document);
        }
    }
}