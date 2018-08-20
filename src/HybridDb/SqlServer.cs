using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;

namespace HybridDb
{
    public class SqlServer : IDatabase
    {
        readonly DocumentStore store;
        readonly string connectionString;
        readonly string prefix;

        public SqlServer(DocumentStore store, string connectionString, string prefix)
        {
            this.store = store;
            this.connectionString = connectionString;
            this.prefix = prefix;

            OnMessage = message => { };
        }

        public Action<SqlInfoMessageEventArgs> OnMessage { get; set; }

        public string FormatTableName(string tablename) => $"{prefix}{tablename}";
        public string FormatTableNameAndEscape(string tablename) => Escape(FormatTableName(tablename));
        public string Escape(string identifier) => $"[{identifier}]";

        public ManagedConnection Connect()
        {
            Action complete = () => { };
            Action dispose = () => { };

            try
            {
                if (Transaction.Current == null)
                {
                    var tx = new TransactionScope(
                        TransactionScopeOption.RequiresNew,
                        new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

                    complete += tx.Complete;
                    dispose += tx.Dispose;
                }

                var connection = new SqlConnection(connectionString);

                complete = connection.Dispose + complete;
                dispose = connection.Dispose + dispose;

                connection.InfoMessage += (obj, args) => OnMessage(args);
                connection.Open();

                connection.EnlistTransaction(Transaction.Current);

                return new ManagedConnection(connection, complete, dispose);
            }
            catch (Exception)
            {
                dispose();
                throw;
            }
        }

        public void DropTables(IEnumerable<string> tables)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                foreach (var table in tables)
                {
                    var tableName = FormatTableNameAndEscape(table);

                    connection.Execute($@"
                        if (object_id('{tableName}', 'U') is not null) 
                        begin
                            drop table {tableName};
                        end");
                }
            }
        }

        public Dictionary<string, List<string>> QuerySchema()
        {
            var schema = new Dictionary<string, List<string>>();

            using (var managedConnection = Connect())
            {
                var columns = managedConnection.Connection.Query<TableInfo, QueryColumn, Tuple<TableInfo, QueryColumn>>($@"
SELECT 
   table_name = t.table_name,
   full_table_name = t.table_name,
   column_name = c.name,
   type_name = (select name from sys.types where user_type_id = c.user_type_id),
   max_length = c.max_length,
   is_nullable = c.is_nullable,
   default_value = (select column_default from information_schema.columns where table_name=t.table_name and column_name=c.name),
   is_primary_key = (
        select 1 
        from information_schema.table_constraints as ct
        join information_schema.key_column_usage as k
        on ct.table_name = k.table_name
        and ct.constraint_catalog = k.constraint_catalog
        and ct.constraint_schema = k.constraint_schema 
        and ct.constraint_name = k.constraint_name
        where ct.constraint_type = 'primary key'
        and k.table_name = t.table_name
        and k.column_name = c.name)
FROM information_schema.tables AS t
INNER JOIN sys.columns AS c
ON OBJECT_ID(t.table_name) = c.[object_id]
WHERE t.table_type='BASE TABLE' and t.table_name LIKE '{prefix}%'
OPTION (FORCE ORDER);",

                    Tuple.Create, splitOn: "column_name");

                foreach (var columnByTable in columns.GroupBy(x => x.Item1))
                {
                    var tableName = columnByTable.Key.table_name.Remove(0, prefix.Length);

                    schema.Add(tableName, columnByTable.Select(column => column.Item2.column_name).ToList());
                }

                return schema;
            }
        }

        public int RawExecute(string sql, object parameters = null)
        {
            if (parameters is IEnumerable<Parameter> hdbParams)
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
            if (parameters is IEnumerable<Parameter> hdbParams)
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

            protected bool Equals(TableInfo other) => string.Equals(table_name, other.table_name) && string.Equals(full_table_name, other.full_table_name);

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