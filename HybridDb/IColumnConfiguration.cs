using System;
using System.Data;
using System.Data.SqlClient;

namespace HybridDb
{
    public interface IColumnConfiguration
    {
        string Name { get; }
        Column Column { get; }
        object GetValue(object document);
    }

    public class Column
    {
        public Column(DbType dbType, int? length = null)
        {
            DbType = dbType;
            Length = length;
        }

        public int? Length { get; set; }
        public DbType DbType { get; set; }

        public string SqlType
        {
            get
            {
                var length = (Length != null)
                                 ? "(" + (Length == Int32.MaxValue ? "MAX" : Length.ToString()) + ")"
                                 : "";

                return new SqlParameter {DbType = DbType}.SqlDbType + length;
            }
        }
    }
}