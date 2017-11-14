using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class UpdateCommand : DatabaseCommand
    {
        public DocumentTable Table { get; }
        public string Key { get; }
        public Guid CurrentEtag { get; }
        public object Projections { get; }
        public bool LastWriteWins { get; }

        public UpdateCommand(DocumentTable table, string key, Guid etag, object projections, bool lastWriteWins)
        {
            Table = table;
            Key = key;
            CurrentEtag = etag;
            Projections = projections;
            LastWriteWins = lastWriteWins;
        }
    }
}