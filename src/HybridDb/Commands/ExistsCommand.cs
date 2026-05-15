using System;
using Dapper;
using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Commands
{
    public class ExistsCommand : HybridDbCommand<Guid?>
    {
        public ExistsCommand(DocumentTable table, string id)
        {
            Table = table;
            Id = id;
        }

        public string Id { get; }
        public DocumentTable Table { get; }

        public static Guid? Execute(DocumentTransaction tx, ExistsCommand command)
        {
            tx.Store.Stats.NumberOfRequests++;

            var sql = Sql.From($"select {DocumentTable.EtagColumn} from {command.Table} where {DocumentTable.IdColumn} = {command.Id}").Build(tx.Store, out var parameters);

            return (Guid?)tx.SqlConnection.ExecuteScalar(sql, parameters, tx.SqlTransaction);
        }
    }
}