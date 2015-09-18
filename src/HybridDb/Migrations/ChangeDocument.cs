using System;

namespace HybridDb.Migrations
{
    public class ChangeDocument<T> : DocumentMigrationCommand
    {
        readonly Func<IDocumentSession, ISerializer, byte[], byte[]> change;

        public ChangeDocument(Func<ISerializer, byte[], byte[]> change)
        {
            this.change = (_, serializer, json) => change(serializer, json);
        }

        public ChangeDocument(Func<IDocumentSession, ISerializer, byte[], byte[]> change)
        {
            this.change = change;
        }

        public override bool ForType(Type type)
        {
            return typeof (T).IsAssignableFrom(type);
        }

        public override byte[] Execute(IDocumentSession session, ISerializer serializer, byte[] json)
        {
            return change(session, serializer, json);
        }
    }
}