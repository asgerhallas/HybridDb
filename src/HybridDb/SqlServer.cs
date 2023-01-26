using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Logging;

namespace HybridDb
{
    public abstract class SqlServer : IDatabase
    {
        protected readonly DocumentStore store;
        protected readonly string connectionString;

        protected SqlServer(DocumentStore store, string connectionString)
        {
            this.store = store;
            this.connectionString = connectionString;

            OnMessage = message => { };
        }

        public Action<SqlInfoMessageEventArgs> OnMessage { get; set; }

        public virtual void Initialize() { }

        public abstract ManagedConnection Connect(bool schema = false, TimeSpan? connectionTimeout = null);

        public abstract Dictionary<string, List<string>> QuerySchema();
        public abstract string FormatTableName(string tablename);
        public abstract void Dispose();

        public string FormatTableNameAndEscape(string tablename) => Escape(FormatTableName(tablename));

        public string Escape(string identifier) => $"[{identifier}]";

        public int RawExecute(string sql, object parameters = null, bool schema = false, int? commandTimeout = null)
        {
            store.Logger.LogDebug(sql);

            using var connection = Connect(schema);
            
            var result = connection.Connection.Execute(sql, parameters.ToHybridDbParameters(), commandTimeout: commandTimeout);
            
            connection.Complete();

            return result;
        }

        public IEnumerable<T> RawQuery<T>(string sql, object parameters = null, bool schema = false)
        {
            store.Logger.LogDebug(sql);

            using var connection = Connect(schema);
            
            var hybridDbParameters = parameters.ToHybridDbParameters();

            return connection.Connection.Query<T>(sql, hybridDbParameters);
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