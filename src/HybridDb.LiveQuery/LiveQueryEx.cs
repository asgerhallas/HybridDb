using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb.LiveQuery
{
    public static class LiveQueryEx
    {
        public static void UseLiveQuery(this Configuration config, Action<LiveQueryOptions> configure = null)
        {
            var options = new LiveQueryOptions();
            configure?.Invoke(options);

            if (!config.Register(_ => options, overwriteExisting: false))
            {
                throw new HybridDbException("Only one live query can be enabled per store.");
            }

            var changeLogTable = (ChangeLogTable)config.GetOrAddTable(new ChangeLogTable(options.TableName));

            config.Decorate<HybridDbCommandExecutor>((_, decoratee) => (tx, command) =>
            {
                var result = decoratee(tx, command);

                switch (command)
                {
                    case InsertCommand insertCommand:
                        AppendChange(tx, changeLogTable, insertCommand.Table.Name, insertCommand.Id, Operation.Inserted);
                        break;
                    case UpdateCommand updateCommand:
                        AppendChange(tx, changeLogTable, updateCommand.Table.Name, updateCommand.Id, Operation.Updated);
                        break;
                    case DeleteCommand deleteCommand:
                        AppendChange(tx, changeLogTable, deleteCommand.Table.Name, deleteCommand.Key, Operation.Deleted);
                        break;
                }

                return result;
            });
        }

        public static IEnumerable<DocumentChange> QueryChanges(this IDocumentStore store, DocumentTable table, byte[] since = null) =>
            store.Transactionally(tx => tx.QueryChanges(table, since));

        public static IEnumerable<DocumentChange> QueryChanges(this DocumentTransaction tx, DocumentTable table, byte[] since = null)
        {
            var changeLogTable = GetChangeLogTable(tx.Store.Configuration);
            since ??= new byte[8];

            var tableName = table.Name;
            var formattedTableName = tx.Store.Database.FormatTableNameAndEscape(changeLogTable.Name);

            var earliestPosition = tx.SqlConnection.QueryFirstOrDefault<byte[]>(
                $@"select top 1 Position
                   from {formattedTableName}
                   where TableName = @TableName
                   order by Position asc",
                new { TableName = tableName },
                tx.SqlTransaction);

            if (!IsStartPosition(since) && earliestPosition != null && ComparePositions(since, earliestPosition) < 0)
            {
                throw LiveQueryPositionOutOfRangeException.For(table);
            }

            return tx.SqlConnection.Query<Row>(
                    $@"select Position, CreatedAt, TableName, DocumentId, CommitId, Operation
                       from {formattedTableName}
                       where TableName = @TableName
                         and Position > @Since
                         and Position < min_active_rowversion()
                       order by Position asc",
                    new { TableName = tableName, Since = since },
                    tx.SqlTransaction)
                .Select(x => new DocumentChange(x.TableName, x.DocumentId, x.CommitId, (Operation)x.Operation, x.Position, x.CreatedAt))
                .ToList();
        }

        public static int PruneChanges(this IDocumentStore store) =>
            store.PruneChanges(store.Configuration.Resolve<LiveQueryOptions>().RetentionPeriod);

        public static int PruneChanges(this IDocumentStore store, TimeSpan retentionPeriod) =>
            store.PruneChangesOlderThan(DateTimeOffset.UtcNow.Subtract(retentionPeriod));

        public static int PruneChangesOlderThan(this IDocumentStore store, DateTimeOffset cutoff)
        {
            var changeLogTable = GetChangeLogTable(store.Configuration);

            return store.Database.RawExecute(
                $"delete from {store.Database.FormatTableNameAndEscape(changeLogTable.Name)} where CreatedAt < @Cutoff",
                new { Cutoff = cutoff },
                commandTimeout: 300);
        }

        static void AppendChange(DocumentTransaction tx, ChangeLogTable changeLogTable, string tableName, string documentId, Operation operation)
        {
            tx.SqlConnection.Execute(
                $@"insert into {tx.Store.Database.FormatTableNameAndEscape(changeLogTable.Name)}
                    ([CreatedAt], [TableName], [DocumentId], [CommitId], [Operation])
                   values
                    (@CreatedAt, @TableName, @DocumentId, @CommitId, @Operation)",
                new
                {
                    CreatedAt = DateTimeOffset.UtcNow,
                    TableName = tableName,
                    DocumentId = documentId,
                    CommitId = tx.CommitId,
                    Operation = (byte)operation
                },
                tx.SqlTransaction);
        }

        static ChangeLogTable GetChangeLogTable(Configuration configuration) =>
            configuration.Tables.Values.OfType<ChangeLogTable>().SingleOrDefault()
            ?? throw new HybridDbException("Live query is not enabled. Run configuration.UseLiveQuery() when setting up HybridDb.");

        static bool IsStartPosition(byte[] position) => position.Length == 8 && position.All(x => x == 0);

        static int ComparePositions(byte[] left, byte[] right)
        {
            for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
            {
                var comparison = left[i].CompareTo(right[i]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return left.Length.CompareTo(right.Length);
        }

        class Row
        {
            public byte[] Position { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public string TableName { get; set; }
            public string DocumentId { get; set; }
            public Guid CommitId { get; set; }
            public byte Operation { get; set; }
        }
    }
}
