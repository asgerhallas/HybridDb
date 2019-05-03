using System;

namespace HybridDb.Migrations.Documents
{
    public abstract class DocumentMigrationCommand
    {
        public abstract bool ForType(Type type);
        public abstract string Execute(IDocumentSession session, ISerializer serializer, string json);
    }
}