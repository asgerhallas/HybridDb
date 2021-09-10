using System.Collections.Generic;
using System.Linq;
using Dapper;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class GetCommand : Command<IDictionary<string, object>>
    {
        public GetCommand(DocumentTable table, string id)
        {
            Table = table;
            Id = id;
        }

        public string Id { get; }
        public DocumentTable Table { get; }

        public static IDictionary<string, object> Execute(DocumentTransaction tx, GetCommand command)
        {
            tx.Store.Stats.NumberOfRequests++;
            tx.Store.Stats.NumberOfGets++;

            var sql = $"select * from {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)} where {DocumentTable.IdColumn.Name} = @Id";

            // ReSharper disable once RedundantAnonymousTypePropertyName
            return (IDictionary<string, object>)tx.SqlConnection.QueryFirstOrDefault(sql, new { Id = command.Id }, tx.SqlTransaction);
        }
    }
}