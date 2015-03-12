using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using Newtonsoft.Json.Linq;

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

    public abstract class DocumentMigrationCommand
    {
    }

    public abstract class ChangeDocument : DocumentMigrationCommand
    {
        public abstract bool ForType(Type type);
        public abstract void Execute(JObject json);
    }

    public class ChangeDocument<T> : ChangeDocument
    {
        private readonly Action<JObject> change;

        public ChangeDocument(Action<JObject> change)
        {
            this.change = change;
        }

        public override bool ForType(Type type)
        {
            return typeof (T).IsAssignableFrom(type);
        }

        public override void Execute(JObject json)
        {
            change(json);
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