using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace HybridDb.Events.Commands
{
    static class CommandEx
    {
        public static IEnumerable<Commit<byte[]>> Batch(this IEnumerable<Row> rows)
        {
            var currentCommitId = Guid.Empty;
            var currentGeneration = -1;
            var currentPosition = -1L;
            var events = new List<EventData<byte[]>>();

            foreach (var row in rows)
            {
                // first row
                if (currentCommitId == Guid.Empty)
                    currentCommitId = row.CommitId;

                // next commit begun, return the current
                if (row.CommitId != currentCommitId)
                {
                    yield return Commit.Create(currentCommitId, currentGeneration, currentPosition, events);

                    currentCommitId = row.CommitId;
                    events = new List<EventData<byte[]>>();
                }

                currentGeneration = row.Generation;
                currentPosition = row.Position;

                // still same commit
                var metadata = new Metadata(JsonConvert.DeserializeObject<Dictionary<string, string>>(row.Metadata));

                events.Add(new EventData<byte[]>(row.StreamId, row.EventId, row.Name, row.SequenceNumber, metadata, row.Data));
            }

            if (events.Any())
            {
                yield return Commit.Create(currentCommitId, currentGeneration, currentPosition, events);
            }
        }
    }
}