using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace HybridDb.Config
{
    public static class SqlTypeMap
    {
        static readonly List<SqlTypeMapping> sqlTypeMappings;

        static SqlTypeMap()
        {
            sqlTypeMappings = new List<SqlTypeMapping>
            {
                new SqlTypeMapping(typeof (long), DbType.Int64, "bigint"),
                new SqlTypeMapping(typeof (byte[]), DbType.Binary, "binary"),
                new SqlTypeMapping(typeof (bool), DbType.Boolean, "bit"),
                new SqlTypeMapping(typeof (string), DbType.String, "nvarchar"),
                new SqlTypeMapping(typeof (string), DbType.StringFixedLength, "nchar"), //fixed
                new SqlTypeMapping(typeof (Enum), DbType.String, "nvarchar"),
                new SqlTypeMapping(typeof (DateTime), DbType.DateTime2, "datetime2"),
                new SqlTypeMapping(typeof (DateTimeOffset), DbType.DateTimeOffset, "datetimeoffset"),
                new SqlTypeMapping(typeof (decimal), DbType.Decimal, "decimal"),
                new SqlTypeMapping(typeof (int), DbType.Int32, "int"),
                new SqlTypeMapping(typeof (double), DbType.Double, "float"),
                new SqlTypeMapping(typeof (Single), DbType.Single, "real"),
                new SqlTypeMapping(typeof (short), DbType.Int16, "smallint"),
                new SqlTypeMapping(typeof (TimeSpan), DbType.Time, "time"),
                new SqlTypeMapping(typeof (byte), DbType.Byte, "tinyint"),
                new SqlTypeMapping(typeof (Guid), DbType.Guid, "uniqueidentifier"),
                new SqlTypeMapping(typeof (string), DbType.Xml, "xml")
            };          
        }       

        public static IEnumerable<SqlTypeMapping> ForNetType(Type type)
        {
            return sqlTypeMappings.Where(x => x.NetType == type);
        }

        public static IEnumerable<SqlTypeMapping> ForDbType(DbType type)
        {
            return sqlTypeMappings.Where(x => x.DbType == type);
        }

        public static IEnumerable<SqlTypeMapping> ForSqlType(string type)
        {
            return sqlTypeMappings.Where(x => x.SqlType == type);
        }

        public static SqlColumn GetDbType(Column column)
        {
            return new SqlColumn(ForNetType(column.Type).First().DbType, GetLength(column.Length, column.Type));
        }

        static int? GetLength(int? length, Type type)
        {
            if (length != null)
                return length;

            if (type == typeof (string))
                return Int32.MaxValue;
            if (type == typeof(Enum))
                return 255;
            if (type == typeof (byte[]))
                return 255;

            return null;
        }

        public class SqlTypeMapping
        {
            public SqlTypeMapping(Type netType, DbType dbType, string sqlType)
            {
                NetType = netType;
                DbType = dbType;
                SqlType = sqlType;
            }

            public Type NetType { get; private set; }
            public DbType DbType { get; private set; }
            public string SqlType { get; private set; }
        }
    }

    public class SqlColumn
    {
        public SqlColumn(DbType dbType, int? length)
        {
            DbType = dbType;
            Length = length;
        }

        public DbType DbType { get; private set; }
        public int? Length { get; private set; }
    }
}