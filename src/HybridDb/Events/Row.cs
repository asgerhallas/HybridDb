using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HybridDb.Events
{
    public class Row
    {
        public long Position { get; set; }
        public Guid EventId { get; set; }
        public Guid CommitId { get; set; }
        public string StreamId { get; set; }
        public long SequenceNumber { get; set; }
        public string Name { get; set; }
        public int Generation { get; set; }
        public string Metadata { get; set; }
        public byte[] Data { get; set; }

        public static EventData<byte[]> ToEvent(Row row) =>
            new(row.StreamId, row.EventId, row.Name, row.SequenceNumber,
                new Metadata(JsonConvert.DeserializeObject<Dictionary<string, string>>(row.Metadata)), row.Data);
    }
}