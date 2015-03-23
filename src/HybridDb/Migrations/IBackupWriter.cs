using System;
using HybridDb.Config;

namespace HybridDb.Migrations
{
    public interface IBackupWriter
    {
        void Write(DocumentDesign design, Guid id, int version, byte[] document);
    }
}