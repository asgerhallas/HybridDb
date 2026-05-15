using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using HybridDb.SqlBuilder;

namespace HybridDb.Events.Commands
{
    public class ReadEvents : HybridDbCommand<IEnumerable<Commit<byte[]>>>
    {
        public ReadEvents(EventTable table, long fromPositionIncluding, bool readPastActiveTransactions)
        {
            Table = table;
            FromPositionIncluding = fromPositionIncluding;
            ReadPastActiveTransactions = readPastActiveTransactions;
        }

        public EventTable Table { get; }
        public long FromPositionIncluding { get; }
        public bool ReadPastActiveTransactions { get; }

        public static IEnumerable<Commit<byte[]>> Execute(DocumentTransaction tx, ReadEvents command)
        {
            if (tx.SqlTransaction.IsolationLevel != IsolationLevel.Snapshot)
            {
                throw new InvalidOperationException("Reads from event store is best done in snapshot isolation so they don't block writes.");
            }

            var sql = Sql.From($@"
                SELECT Position, EventId, CommitId, StreamId, SequenceNumber, Name, Generation, Metadata, Data
                FROM {command.Table}
                WHERE Position >= @fromPosition {(!command.ReadPastActiveTransactions ? "AND RowVersion < min_active_rowversion()" : ""):@}
                ORDER BY Position ASC").Build(tx.Store, out _);

            return tx.SqlConnection.Query<Row>(sql, new {fromPosition = command.FromPositionIncluding}, tx.SqlTransaction, buffered: false).Batch();
        }
    }
}