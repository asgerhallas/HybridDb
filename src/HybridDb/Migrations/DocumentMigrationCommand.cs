using System;

namespace HybridDb.Migrations
{
    public abstract class DocumentMigrationCommand
    {
        public abstract bool ForType(Type type);
        public abstract byte[] Execute(IDocumentSession session, ISerializer serializer, byte[] json);
    }
}