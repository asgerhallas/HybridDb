using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb
{
    public static class DocumentStoreEx
    {
        public static Guid Insert(this IDocumentStore store, DocumentTable table, string key, object projections) => 
            Execute(store, new InsertCommand(table, key, projections));

        public static Guid Update(this IDocumentStore store, DocumentTable table, string key, Guid etag, object projections, bool lastWriteWins = false) => 
            Execute(store, new UpdateCommand(table, key, etag, projections, lastWriteWins));

        public static void Delete(this IDocumentStore store, DocumentTable table, string key, Guid etag, bool lastWriteWins = false) => 
            Execute(store, new DeleteCommand(table, key, etag, lastWriteWins));

        public static Guid Execute(this IDocumentStore store, params DatabaseCommand[] commands) => store.Execute(commands);

        public static IEnumerable<IDictionary<string, object>> Query(
            this IDocumentStore store, DocumentTable table, out QueryStats stats, string select = null, string where = "",
            int skip = 0, int take = 0, string orderby = "", bool includeDeleted = false, object parameters = null) => 
            store.Query<object>(table, out stats, @select, @where, skip, take, @orderby, includeDeleted, parameters)
                .Select(x => (IDictionary<string, object>) x.Data);

        public static IEnumerable<QueryResult<TProjection>> Query<TProjection>(
            this IDocumentStore documentStore, DocumentTable table, byte[] since, string select = null) =>
            documentStore.Query<TProjection>(table, out _, select,
                where: $"{table.RowVersionColumn.Name} > @Since and {table.RowVersionColumn.Name} < min_active_rowversion()",
                orderby: $"{table.RowVersionColumn.Name} ASC",
                includeDeleted: true,
                parameters: new { Since = since });


    }
}