using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
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
        public abstract Dictionary<string, Table> QuerySchema();
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

        public void RawExecute(string sql, object parameters = null)
        {
            var hdbParams = parameters as IEnumerable<Parameter>;
            if (hdbParams != null)
                parameters = new FastDynamicParameters(hdbParams);

            store.Logger.Debug(sql);

            using (var connection = Connect())
            {
                connection.Connection.Execute(sql, parameters);
                connection.Complete();
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

        protected Column Map(string tableName, QueryColumn sqlcolumn, bool isTempTable)
        {
            var columnType = GetType(sqlcolumn);
            var column = new Column(
                sqlcolumn.Name, columnType, sqlcolumn.max_length,
                GetDefaultValue(columnType, tableName, sqlcolumn, isTempTable),
                IsPrimaryKey(sqlcolumn.Name, isTempTable));

            column.Nullable = sqlcolumn.is_nullable;

            return column;
        }

        Type GetType(QueryColumn column)
        {
            var id = column.system_type_id;
            //https://msdn.microsoft.com/en-us/library/cc716729.aspx
            var rawQuery = RawQuery<string>("select name from sys.types where system_type_id = @id", new {id});

            var shortName = rawQuery.FirstOrDefault();
            if (shortName == null)
                throw new ArgumentOutOfRangeException(string.Format("Found no matching sys.type for typeId '{0}'.", id));

            var firstMatchingType = SqlTypeMap.ForSqlType(shortName).FirstOrDefault();
            if (firstMatchingType == null)
                throw new ArgumentOutOfRangeException(string.Format("Found no matching .NET type for typeName type '{0}'.", shortName));

            return column.is_nullable
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
            var sql =
                "SELECT K.TABLE_NAME, " +
                "K.COLUMN_NAME, " +
                "K.CONSTRAINT_NAME " +
                String.Format("FROM {0}INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C ", dbPrefix) +
                String.Format("JOIN {0}INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K ", dbPrefix) +
                "ON C.TABLE_NAME = K.TABLE_NAME " +
                "AND C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG " +
                "AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA " +
                "AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME " +
                "WHERE C.CONSTRAINT_TYPE = 'PRIMARY KEY' " +
                "AND K.COLUMN_NAME = '" + column + "'";

            var isPrimaryKey = RawQuery<dynamic>(sql).Any();
            return isPrimaryKey;
        }

        object GetDefaultValue(Type columnType, string tableName, QueryColumn column, bool isTempTable)
        {
            var defaultValueInDb = RawQuery<string>(
                String.Format("select column_default from {0}information_schema.columns where table_name='{1}' and column_name='{2}'",
                    isTempTable ? "tempdb." : "",
                    tableName,
                    column.Name)).SingleOrDefault();

            if (defaultValueInDb == null)
                return null;

            var defaultValue = defaultValueInDb.Replace("'", "").Trim('(', ')');
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

        protected class QueryColumn
        {
            public string Name { get; set; }
            public int system_type_id { get; set; }
            public int max_length { get; set; }
            public bool is_nullable { get; set; }
        }
    }
}