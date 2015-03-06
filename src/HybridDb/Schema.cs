using System.Collections.Generic;
using System.Linq;

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

        public bool TableExists(string name)
        {
            if (tableMode == TableMode.UseRealTables)
            {
                return store.RawQuery<dynamic>(string.Format("select OBJECT_ID('{0}') as Result", name)).First().Result != null;
            }
        
            return store.RawQuery<dynamic>(string.Format("select OBJECT_ID('tempdb..{0}') as Result", store.FormatTableName(name))).First().Result != null;
        }

        public List<string> GetTables()
        {
            return tableMode == TableMode.UseRealTables
                ? store.RawQuery<string>("select table_name from information_schema.tables where table_type='BASE TABLE'").ToList()
                : store.RawQuery<string>("select * from tempdb.sys.objects where object_id('tempdb.dbo.' + name, 'U') is not null AND name LIKE '#%'")
                    .ToList();
        }

        public Column GetColumn(string table, string column)
        {
            return tableMode == TableMode.UseRealTables
                ? store.RawQuery<Column>(string.Format("select * from sys.columns where Name = N'{0}' and Object_ID = Object_ID(N'{1}')", column, table)).FirstOrDefault()
                : store.RawQuery<Column>(string.Format("select * from tempdb.sys.columns where Name = N'{0}' and Object_ID = Object_ID(N'tempdb..{1}')", column,
                    store.FormatTableName(table))).FirstOrDefault();
        }

        public string GetType(int id)
        {
            var rawQuery = store.RawQuery<string>("select name from sys.types where system_type_id = @id", new {id});
            return rawQuery.FirstOrDefault();
        }

        public bool IsPrimaryKey(string column)
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

        public class Column
        {
            public string Name { get; set; }
            public int system_type_id { get; set; }
            public int max_length { get; set; }
        }
    }
}