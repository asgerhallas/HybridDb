using System;
using Dapper;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class ExistsCommand : Command<Guid?>
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

            var sql = $"select Etag from {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)} where {DocumentTable.IdColumn.Name} = @Id";

            // ReSharper disable once RedundantAnonymousTypePropertyName
            return (Guid?)tx.SqlConnection.ExecuteScalar(sql, new { Id = command.Id }, tx.SqlTransaction);
        }
    }
}