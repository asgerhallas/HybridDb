using System.Collections.Generic;
using System.Linq;
using Dapper;
using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Commands
{
    public class GetCommand : HybridDbCommand<IDictionary<string, IDictionary<string, object>>>
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
            tx.Store.Stats.NumberOfRequests++;
            tx.Store.Stats.NumberOfGets++;

            var sql = Sql.From($"select * from {command.Table} where {DocumentTable.IdColumn} in @Ids").Build(tx.Store, out _);

            return tx.SqlConnection.Query(sql, new { Ids = command.Ids.ToArray() }, tx.SqlTransaction)
                .Cast<IDictionary<string, object>>()
                .ToDictionary(x => x.Get(DocumentTable.IdColumn), x => x);
        }
    }
}