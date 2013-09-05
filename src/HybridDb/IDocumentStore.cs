using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Migration;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Action<Table, IDictionary<string, object>> OnRead { get; set; }
        Configuration Configuration { get; }
        long NumberOfRequests { get; }
        Guid LastWrittenEtag { get; }
        bool IsInTestMode { get; }
        DocumentDesign<TEntity> Document<TEntity>(string tablename = null);
        void Migrate(Action<ISchemaMigrator> migration);
        void MigrateSchemaToMatchConfiguration(bool safe = true);
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
        string Escape(string name);
        string FormatTableNameAndEscape(string tablename);
        string FormatTableName(string tablename);
        
        void RawExecute(string sql, object param);
        IEnumerable<T> RawQuery<T>(string sql, object param);
    }
}