using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb
{
    public static class DocumentStoreEx
    {
        public static Guid Insert(this IDocumentStore store, DocumentTable table, string key, object projections) =>
            store.Transactionally(tx => tx.Execute(new InsertCommand(table, key, projections)));

        public static Guid Update(this IDocumentStore store, DocumentTable table, string key, Guid? etag, object projections) =>
            store.Transactionally(tx => tx.Execute(new UpdateCommand(table, key, etag, projections)));

        public static Guid Delete(this IDocumentStore store, DocumentTable table, string key, Guid? etag) =>
            store.Transactionally(tx => tx.Execute(new DeleteCommand(table, key, etag)));

        public static T Execute<T>(this IDocumentStore store, Command<T> command) => store.Transactionally(tx => store.Execute(tx, command));

        public static void Execute(this IDocumentStore store, IEnumerable<DmlCommand> commands) => store.Execute(commands.ToArray());
        public static void Execute(this IDocumentStore store, params DmlCommand[] commands) => store.Transactionally(tx =>
        {
            foreach (var command in commands)
            {
                store.Execute(tx, command);
            }
        });

        public static IDictionary<string, object> Get(this IDocumentStore store, DocumentTable table, string key) => store.Transactionally(tx => tx.Get(table, key));

        public static IEnumerable<QueryResult<T>> Query<T>(
            this IDocumentStore store, DocumentTable table, out QueryStats stats, bool top1 = false, string select = null, string where = "",
            Window window = null, string orderby = "", bool includeDeleted = false, object parameters = null)
        {
            using (var tx = store.BeginTransaction())
            {
                var (queryStats, rows) = tx.Query<T>(table, top1, @select, @where, window, @orderby, includeDeleted, parameters);

                tx.Complete();

                stats = queryStats;

                return rows;
            }
        }

        public static IEnumerable<IDictionary<string, object>> Query(
            this IDocumentStore store, DocumentTable table, out QueryStats stats, bool top1 = false, string select = null, string where = "",
            Window window = null, string orderby = "", bool includeDeleted = false, object parameters = null) =>
            store.Query<object>(table, out stats, top1, @select, @where, window, @orderby, includeDeleted, parameters)
                .Select(x => (IDictionary<string, object>) x.Data);

        public static IEnumerable<QueryResult<TProjection>> Query<TProjection>(
            this IDocumentStore store, DocumentTable table, byte[] since, string select = null) =>
            store.Transactionally(tx => tx.Query<TProjection>(table, since, @select));

        public static IEnumerable<QueryResult<TProjection>> Query<TProjection>(
            this DocumentTransaction tx, DocumentTable table, byte[] since, string select = null) =>
            tx.Query<TProjection>(table, false, @select,
                @where: $"{DocumentTable.TimestampColumn.Name} > @Since and {DocumentTable.TimestampColumn.Name} < min_active_rowversion()",
                @orderby: $"{DocumentTable.TimestampColumn.Name} ASC",
                includeDeleted: true,
                parameters: new {Since = since}
            ).rows;

        public static void Transactionally(this IDocumentStore store, Action<DocumentTransaction> func) =>
            Transactionally(store, IsolationLevel.ReadCommitted, func);

        public static void Transactionally(this IDocumentStore store, IsolationLevel isolationLevel, Action<DocumentTransaction> func) =>
            store.Transactionally(isolationLevel, tx =>
            {
                func(tx);
                return (object)null;
            });

        public static T Transactionally<T>(this IDocumentStore store, Func<DocumentTransaction, T> func) => 
            Transactionally(store, IsolationLevel.ReadCommitted, func);

        public static T Transactionally<T>(this IDocumentStore store, IsolationLevel isolationLevel, Func<DocumentTransaction, T> func)
        {
            using (var tx = store.BeginTransaction(isolationLevel))
            {
                var result = func(tx);

                tx.Complete();

                return result;
            }
        }

        public static Task<T> Transactionally<T>(this IDocumentStore store, Func<DocumentTransaction, Task<T>> func) => 
            Transactionally(store, IsolationLevel.ReadCommitted, func);

        public static async Task<T> Transactionally<T>(this IDocumentStore store, IsolationLevel isolationLevel, Func<DocumentTransaction, Task<T>> func)
        {
            using (var tx = store.BeginTransaction(isolationLevel))
            {
                var result = await func(tx);

                tx.Complete();

                return result;
            }
        }

        public static IEnumerable<T> Transactionally<T>(this IDocumentStore store, Func<DocumentTransaction, IEnumerable<T>> func) => 
            Transactionally(store, IsolationLevel.ReadCommitted, func);

        public static IEnumerable<T> Transactionally<T>(this IDocumentStore store, IsolationLevel isolationLevel, Func<DocumentTransaction, IEnumerable<T>> func)
        {
            using (var tx = store.BeginTransaction(isolationLevel))
            {
                foreach (var x in func(tx))
                {
                    yield return x;
                }

                tx.Complete();
            }
        }
    }
}