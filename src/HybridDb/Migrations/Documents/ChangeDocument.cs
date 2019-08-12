using System;
using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class ChangeDocument<T> : DocumentRowMigrationCommand
    {
        readonly Func<IDocumentSession, ISerializer, string, string> change;

        public ChangeDocument(Func<ISerializer, string, string> change) : base(typeof(T), null) => 
            this.change = (_, serializer, json) => change(serializer, json);

        public ChangeDocument(Func<IDocumentSession, ISerializer, string, string> change) : base(typeof(T), null) => this.change = change;

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row)
        {
            row.Set(DocumentTable.DocumentColumn, change(session, serializer, row.Get(DocumentTable.DocumentColumn)));
            return row;
        }
    }
}