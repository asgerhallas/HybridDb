using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb
{
    public static class DocumentStoreExtensions
    {
        public static Guid Insert(this IDocumentStore store, DocumentTable table, Guid key, object projections)
        {
            return Execute(store, new InsertCommand(table, key, projections));
        }

        public static Guid Update(this IDocumentStore store, DocumentTable table, Guid key, Guid etag, object projections, bool lastWriteWins = false)
        {
            return Execute(store, new UpdateCommand(table, key, etag, projections, lastWriteWins));
        }

        public static void Delete(this IDocumentStore store, DocumentTable table, Guid key, Guid etag, bool lastWriteWins = false)
        {
            Execute(store, new DeleteCommand(table, key, etag, lastWriteWins));
        }

        public static Guid Execute(this IDocumentStore store, params DatabaseCommand[] commands)
        {
            return store.Execute(commands);
        }

        public static IEnumerable<IDictionary<string, object>> Query(this IDocumentStore store, DocumentTable table, out QueryStats stats, string select = null, string where = "",
            int skip = 0, int take = 0, string orderby = "", object parameters = null)
        {
            return store.Query<IDictionary<string, object>>(table, out stats, @select, @where, skip, take, @orderby, parameters);
        }
    }
}