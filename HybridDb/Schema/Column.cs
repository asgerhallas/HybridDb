using System;
using System.Data;
using System.Data.SqlClient;

namespace HybridDb.Schema
{
    public class Column
    {
        public Column(DbType dbType, int? length = null, bool isPrimaryKey = false)
        {
            DbType = dbType;
            Length = length;
            IsPrimaryKey = isPrimaryKey;
        }

        public int? Length { get; set; }
        public DbType DbType { get; set; }
        public bool IsPrimaryKey { get; set; }

        public string SqlType
        {
            get
            {
                var length = (Length != null)
                                 ? "(" + (Length == Int32.MaxValue ? "MAX" : Length.ToString()) + ")"
                                 : "";

                return new SqlParameter {DbType = DbType}.SqlDbType + length + " " + (IsPrimaryKey ? "NOT NULL PRIMARY KEY" : "");
            }
        }
    }
}