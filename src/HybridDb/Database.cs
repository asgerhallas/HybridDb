using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using HybridDb.Logging;

namespace HybridDb
{
    public class Database : IDisposable
    {
        readonly ILogger logger;
        readonly string connectionString;
        readonly TableMode tableMode;
        readonly bool testMode;

        int numberOfManagedConnections;
        SqlConnection ambientConnectionForTesting;

        public Database(ILogger logger, string connectionString, TableMode tableMode, bool testMode)
        {
            this.logger = logger;
            this.connectionString = connectionString;
            this.tableMode = tableMode;
            this.testMode = testMode;

            OnMessage = message => { };
        }

        public Action<SqlInfoMessageEventArgs> OnMessage { get; set; }

        public TableMode TableMode
        {
            get { return tableMode; }
        }

        public string FormatTableNameAndEscape(string tablename)
        {
            return Escape(FormatTableName(tablename));
        }

        public string Escape(string identifier)
        {
            return string.Format("[{0}]", identifier);
        }

        public string FormatTableName(string tablename)
        {
            return GetTablePrefix() + tablename;
        }

        public string GetTablePrefix()
        {
            switch (tableMode)
            {
                case TableMode.UseRealTables:
                    return "";
                case TableMode.UseTempTables:
                    return "#";
                case TableMode.UseGlobalTempTables:
                    return "##";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void RawExecute(string sql, object parameters = null)
        {
            var hdbParams = parameters as IEnumerable<Parameter>;
            if (hdbParams != null)
                parameters = new FastDynamicParameters(hdbParams);

            logger.Info(sql);

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

            logger.Info(sql);

            using (var connection = Connect())
            {
                return connection.Connection.Query<T>(sql, parameters);
            }
        }

        internal ManagedConnection Connect()
        {
            Action complete = () => { };
            Action dispose = () => { numberOfManagedConnections--; };

            try
            {
                if (Transaction.Current == null)
                {
                    var tx = new TransactionScope();
                    complete += tx.Complete;
                    dispose += tx.Dispose;
                }

                SqlConnection connection;
                if (testMode)
                {
                    // We don't care about thread safety in test mode
                    if (ambientConnectionForTesting == null)
                    {
                        ambientConnectionForTesting = new SqlConnection(connectionString);
                        ambientConnectionForTesting.InfoMessage += (obj, args) => OnMessage(args);
                        ambientConnectionForTesting.Open();

                    }

                    connection = ambientConnectionForTesting;
                }
                else
                {
                    connection = new SqlConnection(connectionString);
                    connection.InfoMessage += (obj, args) => OnMessage(args);
                    connection.Open();

                    complete = connection.Dispose + complete;
                    dispose = connection.Dispose + dispose;
                }

                // Connections that are kept open during multiple operations (for testing mostly)
                // will not automatically be enlisted in transactions started later, we fix that here.
                // Calling EnlistTransaction on a connection that is already enlisted is a no-op.
                connection.EnlistTransaction(Transaction.Current);

                numberOfManagedConnections++;

                return new ManagedConnection(connection, complete, dispose);
            }
            catch (Exception)
            {
                dispose();
                throw;
            }
        }

        public Dictionary<string, Table> QuerySchema()
        {
            return tableMode == TableMode.UseRealTables
                ? GetRealTableSchema()
                : GetTempTablesSchema();
        }

        Dictionary<string, Table> GetTempTablesSchema()
        {
            var schema = new Dictionary<string, Table>();

            var tempTables = RawQuery<string>("select * from tempdb.sys.objects where object_id('tempdb.dbo.' + name, 'U') is not null AND name LIKE '#%'");
            foreach (var tableName in tempTables)
            {
                var formattedTableName = tableName.Remove(tableName.Length - 12, 12).TrimEnd('_');

                var columns = RawQuery<QueryColumn>(
                    string.Format("select * from tempdb.sys.columns where Object_ID = Object_ID(N'tempdb..{0}')", formattedTableName));

                formattedTableName = formattedTableName.TrimStart('#');
                schema.Add(
                    formattedTableName,
                    new Table(formattedTableName, columns.Select(column => Map(tableName, column, isTempTable: true))));
            }
            return schema;
        }

        Dictionary<string, Table> GetRealTableSchema()
        {
            var schema = new Dictionary<string, Table>();
            var realTables = RawQuery<string>("select table_name from information_schema.tables where table_type='BASE TABLE'").ToList();
            foreach (var tableName in realTables)
            {
                var columns = RawQuery<QueryColumn>(string.Format("select * from sys.columns where Object_ID = Object_ID(N'{0}')", tableName));
                schema.Add(tableName, new Table(tableName, columns.Select(column => Map(tableName, column, isTempTable: false))));
            }
            return schema;
        }

        Column Map(string tableName, QueryColumn column, bool isTempTable)
        {
            var columnType = GetType(column.system_type_id);
            return new Column(column.Name, columnType, length: column.max_length, nullable: column.is_nullable,
                defaultValue: SqlTypeMap.GetDefaultValue(columnType, GetDefaultValue(tableName, column, isTempTable)),
                isPrimaryKey: IsPrimaryKey(column.Name, isTempTable));
        }

        string GetDefaultValue(string tableName, QueryColumn column, bool isTempTable)
        {
            return RawQuery<string>(
                string.Format("select column_default from {0}information_schema.columns where table_name='{1}' and column_name='{2}'",
                    isTempTable ? "tempdb." : "",
                    tableName,
                    column.Name)).SingleOrDefault();
        }

        Type GetType(int id)
        {
            //https://msdn.microsoft.com/en-us/library/cc716729.aspx
            var rawQuery = RawQuery<string>("select name from sys.types where system_type_id = @id", new { id });

            var shortName = rawQuery.FirstOrDefault();
            if (shortName == null)
                throw new ArgumentOutOfRangeException(string.Format("Found no matching sys.type for typeId '{0}'.", id));

            var firstMatchingType = SqlTypeMap.ForSqlType(shortName).FirstOrDefault();
            if (firstMatchingType == null)
                throw new ArgumentOutOfRangeException(string.Format("Found no matching .NET type for typeName type '{0}'.", shortName));

            return firstMatchingType.NetType;
        }

        bool IsPrimaryKey(string column, bool isTempTable)
        {
            var dbPrefix = isTempTable ? "tempdb." : "";
            var sql =
                "SELECT K.TABLE_NAME, " +
                "K.COLUMN_NAME, " +
                "K.CONSTRAINT_NAME " +
                string.Format("FROM {0}INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C ", dbPrefix) +
                string.Format("JOIN {0}INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K ", dbPrefix) +
                "ON C.TABLE_NAME = K.TABLE_NAME " +
                "AND C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG " +
                "AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA " +
                "AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME " +
                "WHERE C.CONSTRAINT_TYPE = 'PRIMARY KEY' " +
                "AND K.COLUMN_NAME = '" + column + "'";

            var isPrimaryKey = RawQuery<dynamic>(sql).Any();
            return isPrimaryKey;
        }

        public void Dispose()
        {
            if (numberOfManagedConnections > 0)
                logger.Warn("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");

            if (ambientConnectionForTesting != null)
                ambientConnectionForTesting.Dispose();
        }

        class QueryColumn
        {
            public string Name { get; set; }
            public int system_type_id { get; set; }
            public int max_length { get; set; }
            public bool is_nullable { get; set; }
        }
    }
}