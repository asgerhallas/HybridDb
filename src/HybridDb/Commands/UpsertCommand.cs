using System;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class UpsertCommand : DatabaseCommand
    {
        public UpsertCommand(DocumentTable table, string id, Guid expectedEtag, object projections)
        {
            Table = table;
            Id = id;
            ExpectedEtag = expectedEtag;
            Projections = projections;
        }

        public string Id { get; }
        public Guid ExpectedEtag { get; }
        public object Projections { get; }
        public DocumentTable Table { get; }
    }
}