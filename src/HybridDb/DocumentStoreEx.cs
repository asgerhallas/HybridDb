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
            store.Transactionally(tx => tx.Execute(new InsertCommand(table, key, projections)));

        public static Guid Update(this IDocumentStore store, DocumentTable table, string key, Guid etag, object projections, bool lastWriteWins = false) =>
            store.Transactionally(tx => tx.Execute(new UpdateCommand(table, key, etag, projections, lastWriteWins)));

        public static Guid Delete(this IDocumentStore store, DocumentTable table, string key, Guid etag, bool lastWriteWins = false) =>
            store.Transactionally(tx => tx.Execute(new DeleteCommand(table, key, etag, lastWriteWins)));

        public static Guid Execute(this IDocumentStore store, params DatabaseCommand[] commands) =>
            store.Transactionally(tx => commands.Aggregate(Guid.Empty, (etag, next) => tx.Execute(next)));

        public static IDictionary<string, object> Get(this IDocumentStore store, DocumentTable table, string key) => store.Transactionally(tx => tx.Get(table, key));

        public static IEnumerable<QueryResult<T>> Query<T>(
            this IDocumentStore store, DocumentTable table, out QueryStats stats, string select = null, string where = "",
            int skip = 0, int take = 0, string orderby = "", bool includeDeleted = false, object parameters = null)
        {
            using (var tx = store.BeginTransaction())
            {
                var (queryStats, rows) = tx.Query<T>(table, @select, @where, skip, take, @orderby, includeDeleted, parameters);

                tx.Complete();

                stats = queryStats;

                return rows;
            }
        }

        public static IEnumerable<IDictionary<string, object>> Query(
            this IDocumentStore store, DocumentTable table, out QueryStats stats, string select = null, string where = "",
            int skip = 0, int take = 0, string orderby = "", bool includeDeleted = false, object parameters = null)
        {
            return store.Query<object>(table, out stats, @select, @where, skip, take, @orderby, includeDeleted, parameters)
                .Select(x => (IDictionary<string, object>)x.Data);
        }

        public static IEnumerable<QueryResult<TProjection>> Query<TProjection>(
            this IDocumentStore store, DocumentTable table, byte[] since, string select = null)
        {
            var upperBoundary = !store.Testing || store.TableMode == TableMode.UseRealTables 
                ? $"and {table.RowVersionColumn.Name} < min_active_rowversion()" 
                : "";

            return store.Transactionally(tx => tx.Query<TProjection>(table, @select,
                    @where: $"{table.RowVersionColumn.Name} > @Since {upperBoundary}",
                    @orderby: $"{table.RowVersionColumn.Name} ASC",
                    includeDeleted: true,
                    parameters: new {Since = since}
                ).rows
            );
        }

        public static T Transactionally<T>(this IDocumentStore store, Func<IDocumentTransaction, T> func)
        {
            using (var tx = store.BeginTransaction())
            {
                var result = func(tx);

                tx.Complete();

                return result;
            }
        }
    }
}