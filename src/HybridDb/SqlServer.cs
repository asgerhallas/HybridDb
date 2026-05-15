using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Dapper;
using HybridDb.SqlBuilder;
using Microsoft.Extensions.Logging;

namespace HybridDb
{
    public abstract class SqlServer(DocumentStore store, string connectionString) : IDatabase
    {
        protected readonly DocumentStore store = store;
        protected readonly string connectionString = connectionString;

        public Action<SqlInfoMessageEventArgs> OnMessage { get; set; } = message => { };

        public virtual void Initialize() { }

        public abstract ManagedConnection Connect(bool schema = false, TimeSpan? connectionTimeout = null);

        public abstract Dictionary<string, List<string>> QuerySchema();
        public abstract string FormatTableName(string tablename);
        public abstract void Dispose();

        public string FormatTableNameAndEscape(string tablename) => Escape(FormatTableName(tablename));

        public string Escape(string identifier) => $"[{identifier}]";

        public int RawExecute(Sql sql, bool schema = false, int? commandTimeout = null)
        {
            var sqlString = sql.Build(store, out var parameters);

            store.Logger.LogDebug(sqlString);

            using var connection = Connect(schema);
            
            var result = connection.Connection.Execute(sqlString, parameters, commandTimeout: commandTimeout);
            
            connection.Complete();

            return result;
        }

        public IEnumerable<T> RawQuery<T>(Sql sql, bool schema = false)
        {
            var sqlString = sql.Build(store, out var parameters);

            store.Logger.LogDebug(sqlString);

            using var connection = Connect(schema);
            
            return connection.Connection.Query<T>(sqlString, parameters);
        }

        public class TableInfo
        {
            public string table_name { get; set; }
            public string full_table_name { get; set; }

            protected bool Equals(TableInfo other) => 
                string.Equals(table_name, other.table_name) && string.Equals(full_table_name, other.full_table_name);

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TableInfo)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((table_name != null ? table_name.GetHashCode() : 0) * 397) ^ (full_table_name != null ? full_table_name.GetHashCode() : 0);
                }
            }
        }

        protected class QueryColumn
        {
            public string column_name { get; set; }
            public string type_name { get; set; }
            public int max_length { get; set; }
            public bool is_nullable { get; set; }
            public string default_value { get; set; }
            public bool is_primary_key { get; set; }
        }
    }
}