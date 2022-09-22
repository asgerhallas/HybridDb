using System;
using System.Collections.Generic;
using HybridDb.Migrations.Documents;

namespace HybridDb.Tests.Migrations
{
    public class NoopMigration : DocumentRowMigrationCommand
    {
        public NoopMigration(Type type) : base(type)
        {
        }

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row) => row;
    }
}