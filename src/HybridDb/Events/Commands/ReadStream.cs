using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Newtonsoft.Json;

namespace HybridDb.Events.Commands
{
    public class ReadStream : Command<IEnumerable<Commit<byte[]>>>
    {
        public ReadStream(EventTable table, string streamId, long fromStreamSeq, long toPosition = long.MaxValue, Direction direction = Direction.Forward)
        {
            Table = table;
            StreamId = streamId;
            FromStreamSeq = fromStreamSeq;
            ToPosition = toPosition;
            Direction = direction;
        }

        public EventTable Table { get; }
        public string StreamId { get; }
        public long FromStreamSeq { get; }
        public long ToPosition { get; }
        public Direction Direction { get; }

        public static IEnumerable<Commit<byte[]>> Execute(DocumentTransaction tx, ReadStream command)
        {
            if (tx.SqlTransaction.IsolationLevel != IsolationLevel.Snapshot)    
            {
                throw new InvalidOperationException("Reads from event store is best done in snapshot isolation so they don't block writes.");
            }

            var sql = $@"
                SELECT Position, EventId, CommitId, @Id AS [StreamId], SequenceNumber, Name, Generation, Metadata, Data
                FROM {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}
                WHERE StreamId = @Id AND SequenceNumber >= @fromStreamSeq AND Position <= @toPosition
                ORDER BY SequenceNumber {(command.Direction == Direction.Forward ? "ASC" : "DESC")}";

            // Using DbString over just string as a important performance optimization, 
            // see https://github.com/StackExchange/dapper-dot-net/issues/288
            var idParameter = new DbString {Value = command.StreamId, IsAnsi = false, IsFixedLength = false, Length = 850};

            var currentCommitId = Guid.Empty;
            var currentGeneration = -1;
            var currentPosition = -1L;
            var events = new List<EventData<byte[]>>();

            foreach (var row in tx.SqlConnection.Query<Row>(sql, new { Id = idParameter, command.FromStreamSeq, command.ToPosition }, tx.SqlTransaction, buffered: false))
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