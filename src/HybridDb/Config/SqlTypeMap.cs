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
            if (!ForNetType(column.Type).Any())
                throw new ArgumentException("Can only project .NET simple types, Guid, DateTime, DateTimeOffset, TimeSpan and byte[].");
            
            return new SqlColumn(ForNetType(column.Type).First().DbType, GetLength(column));
        }

        static int? GetLength(Column column)
        {
            if (column.Length != null)
                return column.Length;

            if (column.Type == typeof (string))
                return Int32.MaxValue;
            
            if (column.Type == typeof(Enum))
                return 255;
            
            if (column.Type == typeof (byte[]))
                return Int32.MaxValue;

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
}