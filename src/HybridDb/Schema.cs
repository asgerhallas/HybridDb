using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb
{
    public class Schema : ISchema
    {
        readonly DocumentStore store;
        readonly TableMode tableMode;

        public Schema(DocumentStore store, TableMode tableMode)
        {
            this.store = store;
            this.tableMode = tableMode;
        }

        public Dictionary<string, Table> GetSchema()
        {
            return tableMode == TableMode.UseRealTables 
                ? GetRealTableSchema() 
                : GetTempTablesSchema();
        }

        Dictionary<string, Table> GetTempTablesSchema()
        {
            var schema = new Dictionary<string, Table>();

            var tempTables = store.RawQuery<string>("select * from tempdb.sys.objects where object_id('tempdb.dbo.' + name, 'U') is not null AND name LIKE '#%'");
            foreach (var tableName in tempTables)
            {
                var formattedTableName = tableName.Remove(tableName.Length - 12, 12).TrimEnd('_');

                var columns = store.RawQuery<QueryColumn>(
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
            var realTables = store.RawQuery<string>("select table_name from information_schema.tables where table_type='BASE TABLE'").ToList();
            foreach (var tableName in realTables)
            {
                var columns = store.RawQuery<QueryColumn>(string.Format("select * from sys.columns where Object_ID = Object_ID(N'{0}')", tableName));
                schema.Add(tableName, new Table(tableName, columns.Select(column => Map(tableName, column, isTempTable: false))));
            }
            return schema;
        }

        Column Map(string tableName, QueryColumn column, bool isTempTable)
        {
            var columnType = GetType(column.system_type_id);
            return new Column(column.Name, columnType)
            {
                IsPrimaryKey = IsPrimaryKey(column.Name, isTempTable),
                Length = column.max_length,
                Nullable = column.is_nullable,
                DefaultValue = SqlTypeMap.GetDefaultValue(columnType, GetDefaultValue(tableName, column, isTempTable))

            };
        }

        string GetDefaultValue(string tableName, QueryColumn column, bool isTempTable)
        {
            return store.RawQuery<string>(
                string.Format("select column_default from {0}information_schema.columns where table_name='{1}' and column_name='{2}'",
                    isTempTable ? "tempdb." : "",
                    tableName,
                    column.Name)).SingleOrDefault();
        }

        Type GetType(int id)
        {
            //https://msdn.microsoft.com/en-us/library/cc716729.aspx
            var rawQuery = store.RawQuery<string>("select name from sys.types where system_type_id = @id", new { id });
            
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
                   "SELECT K.TABLE_NAME, "+
                  "K.COLUMN_NAME, "+
                  "K.CONSTRAINT_NAME " +
                  string.Format("FROM {0}INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C ", dbPrefix) +
                  string.Format("JOIN {0}INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K ", dbPrefix)+
                  "ON C.TABLE_NAME = K.TABLE_NAME "+
                  "AND C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG "+
                  "AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA "+
                  "AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME "+
                  "WHERE C.CONSTRAINT_TYPE = 'PRIMARY KEY' "+
                  "AND K.COLUMN_NAME = '" + column + "'";

            var isPrimaryKey = store.RawQuery<dynamic>(sql).Any();
            return isPrimaryKey;
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