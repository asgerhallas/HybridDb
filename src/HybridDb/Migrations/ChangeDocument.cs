using System;

namespace HybridDb.Migrations
{
    public class ChangeDocument<T> : DocumentMigrationCommand
    {
        readonly Func<ISerializer, byte[], byte[]> change;

        public ChangeDocument(Func<ISerializer, byte[], byte[]> change)
        {
            this.change = change;
        }

        public override bool ForType(Type type)
        {
            return typeof (T).IsAssignableFrom(type);
        }

        public override byte[] Execute(ISerializer serializer, byte[] json)
        {
            return change(serializer, json);
        }
    }
}