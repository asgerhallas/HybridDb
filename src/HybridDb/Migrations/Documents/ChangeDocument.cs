using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class ChangeDocument<T> : DocumentRowMigrationCommand
    {
        readonly Func<IDocumentSession, ISerializer, IDictionary<string, object>, string> change;

        public ChangeDocument(Func<ISerializer, string, string> change) : base(typeof(T)) =>
            this.change = (_, serializer, row) => change(serializer, row.Get(DocumentTable.DocumentColumn));

        public ChangeDocument(Func<IDocumentSession, ISerializer, IDictionary<string, object>, string> change) : base(typeof(T)) =>
            this.change = change;

        public ChangeDocument(IReadOnlyList<IDocumentMigrationMatcher> matchers, Func<IDocumentSession, ISerializer, IDictionary<string, object>, string> change)
            : base(typeof(T), matchers.ToArray()) => this.change = change;

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row)
        {
            var migratedDocument = change(session, serializer, row);

            if (migratedDocument != null)
            {
                row.Set(DocumentTable.DocumentColumn, migratedDocument);
                return row;
            }

            row.Add("Deleted", true);
            return row;
        }
    }
}