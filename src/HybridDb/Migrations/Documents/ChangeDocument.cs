using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class ChangeDocument<T> : DocumentRowMigrationCommand
    {
        readonly Func<IDocumentSession, ISerializer, IDictionary<string, object>, string> change;

        public ChangeDocument(Func<ISerializer, string, string> change) : base(typeof(T), null) => 
            this.change = (_, serializer, row) => change(serializer, row.Get(DocumentTable.DocumentColumn));

        public ChangeDocument(Func<IDocumentSession, ISerializer, IDictionary<string, object>, string> change) : base(typeof(T), null) =>
            this.change = change;

        public ChangeDocument(string idPrefix, IReadOnlyList<string> ids, Func<IDocumentSession, ISerializer, IDictionary<string, object>, string> change) 
            : base(typeof(T), idPrefix, ids.ToArray()) => this.change = change;

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row)
        {
            row.Set(DocumentTable.DocumentColumn, change(session, serializer, row));
            return row;
        }
    }
}