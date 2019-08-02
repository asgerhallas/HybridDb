using System;

namespace HybridDb.Migrations.Documents
{
    public abstract class DocumentMigrationCommand
    {
        protected DocumentMigrationCommand(Type type, string idPrefix)
        {
            Type = type;
            IdPrefix = idPrefix;
        }

        public Type Type { get; }
        public string IdPrefix { get; }

        public abstract string Execute(IDocumentSession session, ISerializer serializer, string json);
    }
}