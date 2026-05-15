using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using HybridDb.SqlBuilder;
using Newtonsoft.Json;

namespace HybridDb.Events.Commands
{
    public class ReadStream : HybridDbCommand<IEnumerable<Commit<byte[]>>>
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

            var direction = command.Direction == Direction.Forward ? "ASC" : "DESC";

            var sql = Sql.From($@"
                SELECT Position, EventId, CommitId, @Id AS [StreamId], SequenceNumber, Name, Generation, Metadata, Data
                FROM {command.Table}
                WHERE StreamId = @Id AND SequenceNumber >= @fromStreamSeq AND Position <= @toPosition
                ORDER BY SequenceNumber {direction:@}").Build(tx.Store, out _);

            // Using DbString over just string as an important performance optimization, 
            // see https://github.com/StackExchange/dapper-dot-net/issues/288
            var idParameter = new DbString {Value = command.StreamId, IsAnsi = false, IsFixedLength = false, Length = 850};

            return tx.SqlConnection.Query<Row>(sql, new {Id = idParameter, command.FromStreamSeq, command.ToPosition}, tx.SqlTransaction, buffered: false).Batch();
        }
    }
}