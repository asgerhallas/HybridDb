using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Configuration;
using HybridDb.Migration;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Action<Table, IDictionary<string, object>> OnRead { get; set; }
        ISchema Schema { get; }
        Configuration.Configuration Configuration { get; }
        long NumberOfRequests { get; }
        Guid LastWrittenEtag { get; }
        void Migrate(Action<ISchemaMigrator> migration);
        void LoadExtensions(string path, Func<IHybridDbExtension, bool> predicate);
        void RegisterExtension(IHybridDbExtension hybridDbExtension);
        IDocumentSession OpenSession();
        Guid Execute(params DatabaseCommand[] commands);
        Guid Insert(Table table, Guid key, object projections);
        Guid Update(Table table, Guid key, Guid etag, object projections, bool lastWriteWins = false);
        void Delete(Table table, Guid key, Guid etag, bool lastWriteWins = false);
        IDictionary<string, object> Get(Table table, Guid key);
        IEnumerable<IDictionary<string, object>> Query(Table table, out QueryStats stats, string select = "", string where = "", int skip = 0, int take = 0, string orderby = "", object parameters = null);
        IEnumerable<TProjection> Query<TProjection>(Table table, out QueryStats stats, string select = "", string where = "", int skip = 0, int take = 0, string orderby = "", object parameters = null);
    }
}