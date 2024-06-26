using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    public class DeleteDocuments<T> : DocumentRowMigrationCommand
    {
        public DeleteDocuments(params IDocumentMigrationMatcher[] matchers) : base(typeof(T), matchers) { }

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row)
        {
            row.Set(DocumentTable.DocumentColumn, DocumentMigrator.DeletedDocument.Identifier);
            
            return row;
        }
    }
}