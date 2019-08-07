using System;
using System.Collections.Generic;

namespace HybridDb.Migrations.Documents
{
    public class ChangeDocument<T> : RowMigrationCommand
    {
        readonly Func<IDocumentSession, ISerializer, string, string> change;

        public ChangeDocument(Func<ISerializer, string, string> change) : base(typeof(T), null) => 
            this.change = (_, serializer, json) => change(serializer, json);

        public ChangeDocument(Func<IDocumentSession, ISerializer, string, string> change) : base(typeof(T), null) => this.change = change;

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row)
        {
            row["Document"] = change(session, serializer, (string) row["Document"]);
            return row;
        }
    }
}