using System;
using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb
{
    public interface IDatabase : IDisposable
    {
        ManagedConnection Connect();
        Dictionary<string, Table> QuerySchema();
        string FormatTableNameAndEscape(string tablename);
        string Escape(string identifier);
        string FormatTableName(string tablename);
        int RawExecute(string sql, object parameters = null);
        IEnumerable<T> RawQuery<T>(string sql, object parameters = null);
    }
}