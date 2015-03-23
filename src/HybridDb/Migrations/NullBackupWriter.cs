using System;
using HybridDb.Config;

namespace HybridDb.Migrations
{
    public class NullBackupWriter : IBackupWriter
    {
        public void Write(DocumentDesign design, Guid id, int version, byte[] document)
        {
        }
    }
}