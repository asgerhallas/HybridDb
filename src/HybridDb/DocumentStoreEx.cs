using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb
{
    public static class DocumentStoreEx
    {
        public static Guid Insert(this IDocumentStore store, DocumentTable table, string key, object projections) => 
            store.InsertAsync(table, key, projections).Result;
        public static Task<Guid> InsertAsync(this IDocumentStore store, DocumentTable table, string key, object projections) =>
            ExecuteAsync(store, new InsertCommand(table, key, projections));

        public static Guid Update(this IDocumentStore store, DocumentTable table, string key, Guid etag, object projections, bool lastWriteWins = false) =>
            store.UpdateAsync(table, key, etag, projections, lastWriteWins).Result;
        public static Task<Guid> UpdateAsync(this IDocumentStore store, DocumentTable table, string key, Guid etag, object projections, bool lastWriteWins = false) =>
            ExecuteAsync(store, new UpdateCommand(table, key, etag, projections, lastWriteWins));

        public static void Delete(this IDocumentStore store, DocumentTable table, string key, Guid etag, bool lastWriteWins = false) =>
            store.DeleteAsync(table, key, etag, lastWriteWins).Wait();
        public static Task DeleteAsync(this IDocumentStore store, DocumentTable table, string key, Guid etag, bool lastWriteWins = false) =>
            ExecuteAsync(store, new DeleteCommand(table, key, etag, lastWriteWins));

        public static Guid Execute(this IDocumentStore store, params DatabaseCommand[] commands) => store.ExecuteAsync(commands).Result;
        public static Task<Guid> ExecuteAsync(this IDocumentStore store, params DatabaseCommand[] commands) => store.ExecuteAsync(commands);

        public static Guid Execute(this IDocumentStore store, IEnumerable<DatabaseCommand> commands) => store.ExecuteAsync(commands).Result;

        public static IEnumerable<IDictionary<string, object>> Query(
            this IDocumentStore store, DocumentTable table, out QueryStats stats, string select = null, string where = "",
            int skip = 0, int take = 0, string orderby = "", bool includeDeleted = false, object parameters = null) =>
            store.Query<object>(table, out stats, @select, @where, skip, take, @orderby, includeDeleted, parameters)
                .Select(x => (IDictionary<string, object>) x.Data);

        public static IEnumerable<QueryResult<TProjection>> Query<TProjection>(
            this IDocumentStore documentStore, DocumentTable table, byte[] since, string select = null) =>
            documentStore.Query<TProjection>(table, out _, select,
                where: $"{table.RowVersionColumn.Name} > @Since",
                orderby: $"{table.RowVersionColumn.Name} ASC",
                includeDeleted: true,
                parameters: new {Since = since});
    }
}