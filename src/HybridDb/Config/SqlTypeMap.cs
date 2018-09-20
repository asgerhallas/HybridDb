using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace HybridDb.Config
{
    public static class SqlTypeMap
    {
        static readonly List<SqlTypeMapping> sqlTypeMappings;

        static SqlTypeMap() => sqlTypeMappings = new List<SqlTypeMapping>
        {
            new SqlTypeMapping(typeof (long), SqlDbType.BigInt, "bigint"),
            new SqlTypeMapping(typeof (byte[]), SqlDbType.VarBinary, "varbinary"),
            new SqlTypeMapping(typeof (bool), SqlDbType.Bit, "bit"),
            new SqlTypeMapping(typeof (string), SqlDbType.NVarChar, "nvarchar"),
            new SqlTypeMapping(typeof (Enum), SqlDbType.NVarChar, "nvarchar"),
            new SqlTypeMapping(typeof (DateTime), SqlDbType.DateTime2, "datetime2"),
            new SqlTypeMapping(typeof (DateTimeOffset), SqlDbType.DateTimeOffset, "datetimeoffset"),
            new SqlTypeMapping(typeof (decimal), SqlDbType.Decimal, "decimal"),
            new SqlTypeMapping(typeof (int), SqlDbType.Int, "int"),
            new SqlTypeMapping(typeof (double), SqlDbType.Float, "float"),
            new SqlTypeMapping(typeof (float), SqlDbType.Real, "real"),
            new SqlTypeMapping(typeof (short), SqlDbType.SmallInt, "smallint"),
            new SqlTypeMapping(typeof (byte), SqlDbType.TinyInt, "tinyint"),
            new SqlTypeMapping(typeof (Guid), SqlDbType.UniqueIdentifier, "uniqueidentifier")
        };

        public static SqlTypeMapping ForNetType(Type type)
        {
            return sqlTypeMappings.SingleOrDefault(x => x.NetType == type);
        }

        public static SqlTypeMapping ForSqlType(string type)
        {
            return sqlTypeMappings.FirstOrDefault(x => x.SqlType == type);
        }

        public static SqlColumn Convert(Column column)
        {
            var sqlTypeMapping = ForNetType(column.Type);

            if (sqlTypeMapping == null)
                throw new ArgumentException("Can only project .NET simple types, Guid, DateTime, DateTimeOffset, TimeSpan and byte[].");
            
            return new SqlColumn(sqlTypeMapping.DbType, GetLength(column));
        }

        static string GetLength(Column column)
        {
            if (column.Length != null)
                return column.Length == -1 ? "MAX" : column.Length.ToString();

            if (column.Type == typeof (string))
                return "850";
            
            if (column.Type == typeof(Enum))
                return "255";
            
            if (column.Type == typeof (byte[]))
                return "MAX";

            if (column.Type == typeof (decimal))
                return "28, 14";

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

            public Type NetType { get; private set; }
            public SqlDbType DbType { get; private set; }
            public string SqlType { get; private set; }
        }
    }
}