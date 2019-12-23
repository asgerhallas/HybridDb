using System;
using System.Collections.Generic;
using System.Data;
using Dapper;

namespace HybridDb.Events.Commands
{
    public class ReadEvents : Command<IEnumerable<Commit<byte[]>>>
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

            var sql = $@"
                SELECT Position, EventId, CommitId, StreamId, SequenceNumber, Name, Generation, Metadata, Data
                FROM {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}
                WHERE Position >= @fromPosition {(!command.ReadPastActiveTransactions ? "AND RowVersion < min_active_rowversion()" : "")}
                ORDER BY Position ASC";

            return tx.SqlConnection.Query<Row>(sql, new {fromPosition = command.FromPositionIncluding}, tx.SqlTransaction, buffered: false).Batch();
        }
    }
}