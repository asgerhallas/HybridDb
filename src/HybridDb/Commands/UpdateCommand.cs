using System;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class UpdateCommand : DatabaseCommand
    {
        public DocumentTable Table { get; }
        public string Id { get; }
        public Guid ExpectedEtag { get; }
        public object Projections { get; }
        public bool LastWriteWins { get; }

        public UpdateCommand(DocumentTable table, string id, Guid etag, object projections, bool lastWriteWins)
        {
            Table = table;
            Id = id;
            ExpectedEtag = etag;
            Projections = projections;
            LastWriteWins = lastWriteWins;
        }
    }
}