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
            var schema = new Dictionary<string, Table>();
            if (tableMode == TableMode.UseRealTables)
            {
                var realTables = store.RawQuery<string>("select table_name from information_schema.tables where table_type='BASE TABLE'").ToList();
                foreach (var tableName in realTables)
                {
                    Func<string, string> getDefaultValue =
                        columnName => store.RawQuery<string>(
                            string.Format("select column_default from information_schema.columns where table_name='{0}' and column_name='{1}'", tableName, columnName)).SingleOrDefault();
                     
                    var columns = store.RawQuery<QueryColumn>(string.Format("select * from sys.columns where Object_ID = Object_ID(N'{0}')", tableName));
                    schema.Add(tableName, new Table(tableName, columns.Select(x => Map(x, getDefaultValue))));
                }            
            }

            var tempTables = store.RawQuery<string>("select * from tempdb.sys.objects where object_id('tempdb.dbo.' + name, 'U') is not null AND name LIKE '#%'");
            foreach (var tableName in tempTables)
            {
                var formattedTableName = tableName.Remove(tableName.Length - 12, 12).TrimEnd('_');

                Func<string, string> getDefaultValue =
                    columnName => store.RawQuery<string>(
                        string.Format("select column_default from tempdb.information_schema.columns where table_name='{0}' and column_name='{1}'", tableName, columnName)).SingleOrDefault();

                var columns = store.RawQuery<QueryColumn>(
                    string.Format("select * from tempdb.sys.columns where Object_ID = Object_ID(N'tempdb..{0}')", formattedTableName));

                formattedTableName = formattedTableName.TrimStart('#');
                schema.Add(formattedTableName, new Table(formattedTableName, columns.Select(x => Map(x, getDefaultValue))));

            }
            return schema;
        }

        Column Map(QueryColumn column, Func<string, string> getDefaultValue)
        {
            var defaultValue = getDefaultValue(column.Name);
            var columnType = GetType(column.system_type_id);

            return new Column(column.Name, columnType)
            {
                IsPrimaryKey = IsPrimaryKey(column.Name),
                Length = column.max_length,
                Nullable = column.is_nullable,
                DefaultValue = SqlTypeMap.GetDefaultValue(columnType, defaultValue)

            };
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

        bool IsPrimaryKey(string column)
        {
            var sql =
                @"SELECT K.TABLE_NAME,
                  K.COLUMN_NAME,
                  K.CONSTRAINT_NAME
                  FROM tempdb.INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C
                  JOIN tempdb.INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K
                  ON C.TABLE_NAME = K.TABLE_NAME
                  AND C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG
                  AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA
                  AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME
                  WHERE C.CONSTRAINT_TYPE = 'PRIMARY KEY'
                  AND K.COLUMN_NAME = '" + column + "'";

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