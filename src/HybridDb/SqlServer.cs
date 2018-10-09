using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;

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

        public abstract ManagedConnection Connect();
        public abstract Dictionary<string, List<string>> QuerySchema();
        public abstract string FormatTableName(string tablename);
        public abstract void Dispose();

        public string FormatTableNameAndEscape(string tablename)
        {
            return Escape(FormatTableName(tablename));
        }

        public string Escape(string identifier)
        {
            return string.Format("[{0}]", identifier);
        }

        public int RawExecute(string sql, object parameters = null)
        {
            var hdbParams = parameters as IEnumerable<Parameter>;
            if (hdbParams != null)
                parameters = new FastDynamicParameters(hdbParams);

            store.Logger.Debug(sql);

            using (var connection = Connect())
            {
                var result = connection.Connection.Execute(sql, parameters);
                connection.Complete();

                return result;
            }
        }

        public IEnumerable<T> RawQuery<T>(string sql, object parameters = null)
        {
            var hdbParams = parameters as IEnumerable<Parameter>;
            if (hdbParams != null)
                parameters = new FastDynamicParameters(hdbParams);

            store.Logger.Debug(sql);

            using (var connection = Connect())
            {
                return connection.Connection.Query<T>(sql, parameters);
            }
        }

        protected Column Map(string fullTableName, QueryColumn sqlcolumn)
        {
            var columnType = GetType(sqlcolumn.type_name, sqlcolumn.is_nullable);
            var defaultValue = GetDefaultValue(columnType, sqlcolumn);
            var isPrimaryKey = sqlcolumn.is_primary_key;

            var column = new Column(sqlcolumn.column_name, columnType, sqlcolumn.max_length, defaultValue, isPrimaryKey)
            {
                Nullable = sqlcolumn.is_nullable
            };

            return column;
        }

        Type GetType(string sqlName, bool isNullable)
        {
            var firstMatchingType = SqlTypeMap.ForSqlType(sqlName);
            if (firstMatchingType == null)
                throw new ArgumentOutOfRangeException($"Found no matching .NET type for typeName type '{sqlName}'.");

            return isNullable
                ? GetNullableType(firstMatchingType.NetType)
                : firstMatchingType.NetType;
        }

        static Type GetNullableType(Type type)
        {
            if (type.IsNullable())
                return type;

            return type.IsValueType
                ? typeof (Nullable<>).MakeGenericType(type)
                : type;
        }

        bool IsPrimaryKey(string column, bool isTempTable)
        {
            var dbPrefix = isTempTable ? "tempdb." : "";
            var sql = $@"
select k.table_name, 
k.column_name,
k.constraint_name 
from {dbPrefix}information_schema.table_constraints as c
join {dbPrefix}information_schema.key_column_usage as k
on c.table_name = k.table_name
and c.constraint_catalog = k.constraint_catalog
and c.constraint_schema = k.constraint_schema 
and c.constraint_name = k.constraint_name
where c.constraint_type = 'primary key'
and k.column_name = '{column}'";

            var isPrimaryKey = RawQuery<dynamic>(sql).Any();
            return isPrimaryKey;
        }

        object GetDefaultValue(Type columnType, QueryColumn column)
        {
            if (column.default_value == null)
                return null;

            var defaultValue = column.default_value.Replace("'", "").Trim('(', ')');
            columnType = Nullable.GetUnderlyingType(columnType) ?? columnType;

            if (columnType == typeof (string) || columnType == typeof (Enum))
                return defaultValue;

            if (columnType == typeof (DateTimeOffset))
                return DateTimeOffset.Parse(defaultValue);

            if (columnType == typeof (Guid))
                return Guid.Parse(defaultValue);

            //For legacy support of default boolean values persisted as 0/1
            if (columnType == typeof (bool))
            {
                if (defaultValue == "0")
                    return false;
                if (defaultValue == "1")
                    return true;
            }

            return Convert.ChangeType(defaultValue, columnType);
        }

        public class TableInfo
        {
            public string table_name { get; set; }
            public string full_table_name { get; set; }

            protected bool Equals(TableInfo other)
            {
                return string.Equals(table_name, other.table_name) && string.Equals(full_table_name, other.full_table_name);
            }

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