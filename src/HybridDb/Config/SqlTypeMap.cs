using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace HybridDb.Config
{
    public static class SqlTypeMap
    {
        static readonly List<SqlTypeMapping> sqlTypeMappings;

        static SqlTypeMap() => sqlTypeMappings =
        [
            new(typeof(long), SqlDbType.BigInt, "bigint"),
            new(typeof(byte[]), SqlDbType.VarBinary, "varbinary"),
            new(typeof(bool), SqlDbType.Bit, "bit"),
            new(typeof(string), SqlDbType.NVarChar, "nvarchar"),
            new(typeof(Enum), SqlDbType.NVarChar, "nvarchar"),
            new(typeof(DateTime), SqlDbType.DateTime2, "datetime2"),
            new(typeof(DateTimeOffset), SqlDbType.DateTimeOffset, "datetimeoffset"),
            new(typeof(DateOnly), SqlDbType.Date, "date"),
            new(typeof(decimal), SqlDbType.Decimal, "decimal"),
            new(typeof(int), SqlDbType.Int, "int"),
            new(typeof(double), SqlDbType.Float, "float"),
            new(typeof(float), SqlDbType.Real, "real"),
            new(typeof(short), SqlDbType.SmallInt, "smallint"),
            new(typeof(byte), SqlDbType.TinyInt, "tinyint"),
            new(typeof(Guid), SqlDbType.UniqueIdentifier, "uniqueidentifier")
        ];

        public static SqlTypeMapping ForNetType(Type type)
        {
            var checkType = type.IsEnum ? typeof(Enum) : type;

            return sqlTypeMappings.SingleOrDefault(x => x.NetType == checkType);
        }

        public static SqlTypeMapping ForSqlType(string type) => sqlTypeMappings.FirstOrDefault(x => x.SqlType == type);

        public static SqlColumn Convert(Column column)
        {
            var sqlTypeMapping = ForNetType(column.Type);

            return sqlTypeMapping != null
                ? new SqlColumn(sqlTypeMapping.DbType, GetLength(column))
                : throw new ArgumentException("Can only project .NET simple types, Guid, DateTime, DateTimeOffset, TimeSpan and byte[].");
        }

        static string GetLength(Column column)
        {
            if (column.Length != null)
            {
                return column.Length == -1 ? "MAX" : column.Length.ToString();
            }

            if (column.Type == typeof(string))
            {
                return "850";
            }

            if (column.Type == typeof(Enum))
            {
                return "255";
            }

            if (column.Type == typeof(byte[]))
            {
                return "MAX";
            }

            if (column.Type == typeof(decimal))
            {
                return "28, 14";
            }

            return null;
        }

        public class SqlTypeMapping
        {
            public SqlTypeMapping(Type netType, SqlDbType dbType, string sqlType)
            {
                NetType = netType;
                DbType = dbType;
                SqlType = sqlType;
            }

            public Type NetType { get; }
            public SqlDbType DbType { get; }
            public string SqlType { get; }
        }
    }
}