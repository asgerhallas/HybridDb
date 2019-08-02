using System;

namespace HybridDb.Migrations.Documents
{
    public class ChangeDocument<T> : DocumentMigrationCommand
    {
        readonly Func<IDocumentSession, ISerializer, string, string> change;

        public ChangeDocument(Func<ISerializer, string, string> change) : base(typeof(T), null) => 
            this.change = (_, serializer, json) => change(serializer, json);

        public ChangeDocument(Func<IDocumentSession, ISerializer, string, string> change) : base(typeof(T), null) => this.change = change;

        public override string Execute(IDocumentSession session, ISerializer serializer, string json) => change(session, serializer, json);
    }
}