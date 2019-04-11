using System;

namespace HybridDb.Events
{
    public class Row
    {
        public long Position { get; set; }
        public Guid EventId { get; set; }
        public Guid CommitId { get; set; }
        public string StreamId { get; set; }
        public long Seq { get; set; }
        public string Name { get; set; }
        public string Generation { get; set; }
        public string Metadata { get; set; }
        public byte[] Data { get; set; }
    }
}