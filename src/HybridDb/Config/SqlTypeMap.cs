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
                new SqlTypeMapping(typeof (TimeSpan), DbType.Time, "XXX9"),
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

        public static object GetDefaultValue(Type columnType, string defaultValue)
        {
            if (defaultValue == null)
                return null;
            
            defaultValue = defaultValue.Replace("'", "").Trim('(', ')');
            
            if (columnType == typeof(string))
                return defaultValue;
            if (columnType == typeof (long))
                return Convert.ToInt64(defaultValue);
            if (columnType == typeof (byte[]))
                return GetBytes(defaultValue);
            if (columnType == typeof (bool))
                return Convert.ToBoolean(defaultValue);
            if (columnType == typeof (Enum))
                return defaultValue;            //OBS: correct?
            if (columnType == typeof (DateTime))
                return Convert.ToDateTime(defaultValue);
            if (columnType == typeof (DateTimeOffset))
                return new DateTimeOffset(Convert.ToDateTime(defaultValue));
            if (columnType == typeof (decimal))
                return Convert.ToDecimal(defaultValue);
            if (columnType == typeof (int))
                return Convert.ToInt32(defaultValue);
            if (columnType == typeof (double))
                return Convert.ToDouble(defaultValue);
            if (columnType == typeof (Single))
                return Convert.ToSingle(defaultValue);
            if (columnType == typeof (short))
                return Convert.ToInt16(defaultValue);
            if (columnType == typeof (TimeSpan))
                return TimeSpan.Parse(defaultValue);
            if (columnType == typeof (byte))
                return Convert.ToByte(defaultValue);
            if (columnType == typeof (Guid))
                return Guid.Parse(defaultValue);

            throw new ArgumentException(string.Format("Column type {0} is unknown.", columnType));
        }

        static byte[] GetBytes(string str)
        {
            var bytes = new byte[str.Length * sizeof(char)];
            Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
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