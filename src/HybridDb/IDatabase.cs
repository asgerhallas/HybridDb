using System;
using System.Collections.Generic;
using HybridDb.SqlBuilder;

namespace HybridDb
{
    public interface IDatabase : IDisposable
    {
        void Initialize();

        ManagedConnection Connect(bool schema = false, TimeSpan? connectionTimeout = null);

        Dictionary<string, List<string>> QuerySchema();

        string Escape(string identifier);
        string FormatTableName(string tablename);
        string FormatTableNameAndEscape(string tablename);

        int RawExecute(Sql sql, bool schema = false, int? commandTimeout = null);
        IEnumerable<T> RawQuery<T>(Sql sql, bool schema = false);
    }
}