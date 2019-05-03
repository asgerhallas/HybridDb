using System;

namespace HybridDb.Migrations.Documents
{
    public class ChangeDocument<T> : DocumentMigrationCommand
    {
        readonly Func<IDocumentSession, ISerializer, string, string> change;

        public ChangeDocument(Func<ISerializer, string, string> change)
        {
            this.change = (_, serializer, json) => change(serializer, json);
        }

        public ChangeDocument(Func<IDocumentSession, ISerializer, string, string> change)
        {
            this.change = change;
        }

        public override bool ForType(Type type)
        {
            return typeof (T).IsAssignableFrom(type);
        }

        public override string Execute(IDocumentSession session, ISerializer serializer, string json)
        {
            return change(session, serializer, json);
        }
    }
}