using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Migration;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IAddIn
    {
        void OnRead(Dictionary<string, object> projections);
    }

    public interface IDocumentStore : IDisposable
    {
        Action<Dictionary<string, object>> OnRead { get; set; }
        Configuration Configuration { get; }
        long NumberOfRequests { get; }
        Guid LastWrittenEtag { get; }
        bool IsInTestMode { get; }
        void MigrateSchema(bool safe = true);
        void LoadAddIns();
        IDocumentSession OpenSession();
        DocumentConfiguration<TEntity> Document<TEntity>();
        Guid Execute(params DatabaseCommand[] commands);
        Guid Insert(Table table, Guid key, byte[] document, object projections);
        Guid Update(Table table, Guid key, Guid etag, byte[] document, object projections, bool lastWriteWins = false);
        void Delete(Table table, Guid key, Guid etag, bool lastWriteWins = false);
        IDictionary<Column, object> Get(Table table, Guid key);
        IEnumerable<IDictionary<Column, object>> Query(Table table, out QueryStats stats, string select = "", string where = "", int skip = 0, int take = 0, string orderby = "", object parameters = null);
        IEnumerable<TProjection> Query<TProjection>(Table table, out QueryStats stats, string select = "", string where = "", int skip = 0, int take = 0, string orderby = "", object parameters = null);
        string Escape(string name);
        string FormatTableNameAndEscape(string tablename);
        string FormatTableName(string tablename);
        
        void RawExecute(string sql, object param);
        IEnumerable<T> RawQuery<T>(string sql, object param);
    }
}