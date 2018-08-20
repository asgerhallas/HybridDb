using System;
using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb
{
    public interface IDatabase
    {
        ManagedConnection Connect();
        Dictionary<string, List<string>> QuerySchema();
        string FormatTableNameAndEscape(string tablename);
        string Escape(string identifier);
        string FormatTableName(string tablename);
        int RawExecute(string sql, object parameters = null);
        IEnumerable<T> RawQuery<T>(string sql, object parameters = null);
        void DropTables(IEnumerable<string> tables);
    }
}