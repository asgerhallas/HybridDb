using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using HybridDb.Commands;
using HybridDb.Linq;
using Newtonsoft.Json;

namespace HybridDb.Events.Commands
{
    public class ReadEvents : Command<IEnumerable<Commit<byte[]>>>
    {
        public ReadEvents(EventTable table, long fromPositionIncluding)
        {
            Table = table;
            FromPositionIncluding = fromPositionIncluding;
        }

        public EventTable Table { get; }
        public long FromPositionIncluding { get; }

        public static IEnumerable<Commit<byte[]>> Execute(DocumentTransaction tx, ReadEvents command)
        {
            if (tx.SqlTransaction.IsolationLevel != IsolationLevel.Snapshot)
            {
                throw new InvalidOperationException("Reads from event store is best done in snapshot isolation so they don't block writes.");
            }

            var sql = $@"
                SELECT
                    globSeq AS [Position],
                    id AS [eventId],
                    batch AS [CommitId],
                    name AS [Name],
                    gen AS [Generation],
                    stream AS [StreamId], 
                    seq AS [Seq],
                    data AS [Data], 
                    meta AS [Metadata]
                FROM {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}
                WHERE globSeq >= @fromPosition
                ORDER BY globSeq ASC";

            var currentCommitId = Guid.Empty;
            var currentGeneration = "0.0";
            var currentPosition = -1L;
            var events = new List<EventData<byte[]>>();

            foreach (var row in tx.SqlConnection.Query<Row>(sql, new {fromPosition = command.FromPositionIncluding}, tx.SqlTransaction, buffered: false))
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

                events.Add(new EventData<byte[]>(row.StreamId, row.EventId, row.Name, row.Seq, metadata, row.Data));
            }

            if (events.Any())
            {
                yield return Commit.Create(currentCommitId, currentGeneration, currentPosition, events);
            }
        }
    }
}