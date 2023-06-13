using System;
using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb
{
    public interface IDatabase : IDisposable
    {
        void Initialize();

        ManagedConnection Connect(bool schema = false, TimeSpan? connectionTimeout = null);

        Dictionary<string, List<string>> QuerySchema();

        string Escape(string identifier);
        string FormatTableName(string tableNames);
        string FormatTableNameAndEscape(string tableName);

        int RawExecute(string sql, object parameters = null, bool schema = false, int? commandTimeout = null);
        IEnumerable<T> RawQuery<T>(string sql, object parameters = null, bool schema = false);
    }
}