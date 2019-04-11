using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using HybridDb.Commands;
using Newtonsoft.Json;

namespace HybridDb.Events.Commands
{
    public class ReadStream : Command<IEnumerable<EventData<byte[]>>>
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

        public static IEnumerable<EventData<byte[]>> Execute(DocumentTransaction tx, ReadStream command)
        {
            if (tx.SqlTransaction.IsolationLevel != IsolationLevel.Snapshot)    
            {
                throw new InvalidOperationException("Reads from event store is best done in snapshot isolation so they don't block writes.");
            }

            var sql =
                $@"SELECT
                    globSeq AS [Position],
                    batch as [CommitId],
                    @id AS [StreamId],
                    seq as [Seq],
                    name as [Name],
                    gen as [Generation],
                    data as [Data], 
                    meta as [Metadata]
                  FROM {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}
                  WHERE stream = @id AND seq >= @fromStreamSeq AND globSeq <= @toPosition
                  ORDER BY seq {(command.Direction == Direction.Forward ? "ASC" : "DESC")}";

            // Using DbString over just string as a important performance optimization, 
            // see https://github.com/StackExchange/dapper-dot-net/issues/288
            var idParameter = new DbString {Value = command.StreamId, IsAnsi = false, IsFixedLength = false, Length = 850};

            foreach (var row in tx.SqlConnection.Query<Row>(sql, new {id = idParameter, command.FromStreamSeq, command.ToPosition }, tx.SqlTransaction, buffered: false))
            {
                yield return Row.ToEvent(row);
            }
        }
    }
}