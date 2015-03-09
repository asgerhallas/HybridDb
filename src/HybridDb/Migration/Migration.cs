using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HybridDb.Migration
{
    public abstract class Migration
    {
        protected Migration(int version)
        {
            Version = version;
        }

        public int Version { get; private set; }

        public abstract IEnumerable<MigrationCommand> Migrate();
    }

    public abstract class MigrationCommand
    {
        public abstract void Execute(DocumentStore store);
    }

    public abstract class DocumentMigrationCommand<T> : MigrationCommand
    {

    }

    public class ChangeDocument<T> : DocumentMigrationCommand<T>
    {
        private readonly Action<JObject> change;

        public ChangeDocument(Action<JObject> change)
        {
            this.change = change;
        }

        public override void Execute(DocumentStore store)
        {
            
        }
    }

    public class Reproject<T> : DocumentMigrationCommand<T>
    {
        public override void Execute(DocumentStore store)
        {
            
        }
    }
}