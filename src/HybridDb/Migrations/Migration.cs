using System;
using System.Collections.Generic;

namespace HybridDb.Migrations
{
    public abstract class Migration
    {
        protected Migration(int version)
        {
            Version = version;
        }

        public int Version { get; private set; }

        public virtual IEnumerable<SchemaMigrationCommand> MigrateSchema()
        {
            yield break;
        }

        public virtual IEnumerable<DocumentMigrationCommand> MigrateDocument()
        {
            yield break;
        }
    }

    public abstract class DocumentMigrationCommand {}

    public abstract class ChangeDocument : DocumentMigrationCommand
    {
        public abstract bool ForType(Type type);
        public abstract byte[] Execute(ISerializer serializer, byte[] json);
    }

    public class ChangeDocument<T> : ChangeDocument
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

    public class Reproject<T> : SchemaMigrationCommand
    {
        public override void Execute(Database database)
        {
            // set state?
        }
    }
}