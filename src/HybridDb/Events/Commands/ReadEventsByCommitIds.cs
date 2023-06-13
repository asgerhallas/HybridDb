using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using HybridDb.Commands;

namespace HybridDb.Events.Commands
{
    public class ReadEventsByCommitIds : Command<IEnumerable<Commit<byte[]>>>
    {
        public ReadEventsByCommitIds(EventTable table, params Guid[] ids)
        {
            Table = table;
            Ids = ids;
        }

        public EventTable Table { get; }
        public Guid[] Ids { get; }

        public static IEnumerable<Commit<byte[]>> Execute(DocumentTransaction tx, ReadEventsByCommitIds command)
        {
            if (!command.Ids.Any()) return Enumerable.Empty<Commit<byte[]>>();

            var parameters = new DynamicParameters();

            foreach (var param in command.Ids.Select((value, i) => (name: $"@p{i}", value: value)))
                parameters.Add(param.name, param.value);

            var table = command.Table.GetSpicy(tx.Store);

            var sql = $@"
                SELECT Position, EventId, CommitId, StreamId, SequenceNumber, Name, Generation, Metadata, Data
                FROM {table}
                WHERE CommitId IN ({string.Join(", ", parameters.ParameterNames.Select(x => $"@{x}"))})
                ORDER BY Position";

            var commits = new[] { Commit.Empty<byte[]>() }.Concat(
                from row in tx.SqlConnection.Query<Row>(sql, parameters, tx.SqlTransaction)
                group row by row.CommitId
                into grouping
                let last = grouping.Last()
                select Commit.Create(grouping.Key, last.Generation, last.Position, grouping.Select(Row.ToEvent).ToList())
            ).ToList();

            return
                from id in command.Ids
                join commit in commits on id equals commit.Id into cs
                from commit in cs.DefaultIfEmpty()
                select commit;
        }
    }
}