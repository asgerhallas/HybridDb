using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace HybridDb.Schema
{
    public class SqlColumn
    {
        static readonly Dictionary<Type, SqlColumn> typeToColumn = new Dictionary<Type, SqlColumn>
        {
            {typeof (byte), new SqlColumn(DbType.Byte)},
            {typeof (sbyte), new SqlColumn(DbType.SByte)},
            {typeof (short), new SqlColumn(DbType.Int16)},
            {typeof (ushort), new SqlColumn(DbType.UInt16)},
            {typeof (int), new SqlColumn(DbType.Int32)},
            {typeof (uint), new SqlColumn(DbType.UInt32)},
            {typeof (long), new SqlColumn(DbType.Int64)},
            {typeof (ulong), new SqlColumn(DbType.UInt64)},
            {typeof (float), new SqlColumn(DbType.Single)},
            {typeof (double), new SqlColumn(DbType.Double)},
            {typeof (decimal), new SqlColumn(DbType.Decimal)},
            {typeof (bool), new SqlColumn(DbType.Boolean)},
            {typeof (string), new SqlColumn(DbType.String, Int32.MaxValue)},
            {typeof (char), new SqlColumn(DbType.StringFixedLength)},
            {typeof (Guid), new SqlColumn(DbType.Guid)},
            {typeof (DateTime), new SqlColumn(DbType.DateTime)},
            {typeof (DateTimeOffset), new SqlColumn(DbType.DateTimeOffset)},
            {typeof (TimeSpan), new SqlColumn(DbType.Time)},
            {typeof (Enum), new SqlColumn(DbType.String, Int32.MaxValue)},
            {typeof (byte[]), new SqlColumn(DbType.Binary, Int32.MaxValue)},
            {typeof (byte?), new SqlColumn(DbType.Byte)},
            {typeof (sbyte?), new SqlColumn(DbType.SByte)},
            {typeof (short?), new SqlColumn(DbType.Int16)},
            {typeof (ushort?), new SqlColumn(DbType.UInt16)},
            {typeof (int?), new SqlColumn(DbType.Int32)},
            {typeof (uint?), new SqlColumn(DbType.UInt32)},
            {typeof (long?), new SqlColumn(DbType.Int64)},
            {typeof (ulong?), new SqlColumn(DbType.UInt64)},
            {typeof (float?), new SqlColumn(DbType.Single)},
            {typeof (double?), new SqlColumn(DbType.Double)},
            {typeof (decimal?), new SqlColumn(DbType.Decimal)},
            {typeof (bool?), new SqlColumn(DbType.Boolean)},
            {typeof (char?), new SqlColumn(DbType.StringFixedLength)},
            {typeof (Guid?), new SqlColumn(DbType.Guid)},
            {typeof (DateTime?), new SqlColumn(DbType.DateTime)},
            {typeof (DateTimeOffset?), new SqlColumn(DbType.DateTimeOffset)},
            {typeof (TimeSpan?), new SqlColumn(DbType.Time)},
            //{typeof (object), new SqlColumn(DbType.Object)}
        };

        public SqlColumn(Type type, bool isPrimaryKey = false)
        {
            type = (type.IsEnum) ? type.BaseType : type;
            
            SqlColumn column;
            if (!typeToColumn.TryGetValue(type, out column))
                throw new ArgumentException("Can only project .NET simple types, Guid, DateTime, DateTimeOffset, TimeSpan and byte[].");

            Type = column.Type;
            Length = column.Length;
            IsPrimaryKey = isPrimaryKey;
        }

        public SqlColumn(DbType dbType, int? length = null, bool isPrimaryKey = false)
        {
            Type = dbType;
            Length = length;
            IsPrimaryKey = isPrimaryKey;
        }

        public SqlColumn()
        {
        }

        public int? Length { get; private set; }
        public DbType? Type { get; private set; }
        public bool IsPrimaryKey { get; private set; }
    }
}