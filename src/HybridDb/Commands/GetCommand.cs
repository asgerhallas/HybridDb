using System.Collections.Generic;
using System.Linq;
using Dapper;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class GetCommand : Command<IDictionary<string, IDictionary<string, object>>>
    {
        public GetCommand(DocumentTable table, IReadOnlyList<string> ids)
        {
            Table = table;
            Ids = ids;
        }

        public IReadOnlyList<string> Ids { get; }
        public DocumentTable Table { get; }

        public static IDictionary<string, IDictionary<string, object>> Execute(DocumentTransaction tx, GetCommand command)
        {
            var table = tx.Store.GetTableFor(command.Table);

            tx.Store.Stats.NumberOfRequests++;
            tx.Store.Stats.NumberOfGets++;

            var sql = $"select * from {table} where {DocumentTable.IdColumn.Name} in @Ids";

            return tx.SqlConnection.Query(sql, new { Ids = command.Ids.ToArray() }, tx.SqlTransaction)
                .Cast<IDictionary<string, object>>()
                .ToDictionary(x => x.Get(DocumentTable.IdColumn), x => x);
        }
    }
}