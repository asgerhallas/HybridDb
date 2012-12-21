using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Linq;

namespace HybridDb.Schema
{
    public class ProjectionColumn<TEntity, TMember> : IProjectionColumn
    {
        static readonly Dictionary<Type, Column> typeToColumn = new Dictionary<Type, Column>
        {
            {typeof (byte), new Column(DbType.Byte)},
            {typeof (sbyte), new Column(DbType.SByte)},
            {typeof (short), new Column(DbType.Int16)},
            {typeof (ushort), new Column(DbType.UInt16)},
            {typeof (int), new Column(DbType.Int32)},
            {typeof (uint), new Column(DbType.UInt32)},
            {typeof (long), new Column(DbType.Int64)},
            {typeof (ulong), new Column(DbType.UInt64)},
            {typeof (float), new Column(DbType.Single)},
            {typeof (double), new Column(DbType.Double)},
            {typeof (decimal), new Column(DbType.Decimal)},
            {typeof (bool), new Column(DbType.Boolean)},
            {typeof (string), new Column(DbType.String, Int32.MaxValue)},
            {typeof (char), new Column(DbType.StringFixedLength)},
            {typeof (Guid), new Column(DbType.Guid)},
            {typeof (DateTime), new Column(DbType.DateTime)},
            {typeof (DateTimeOffset), new Column(DbType.DateTimeOffset)},
            {typeof (TimeSpan), new Column(DbType.Time)},
            {typeof (Enum), new Column(DbType.String, Int32.MaxValue)},
            {typeof (byte[]), new Column(DbType.Binary, Int32.MaxValue)},
            {typeof (byte?), new Column(DbType.Byte)},
            {typeof (sbyte?), new Column(DbType.SByte)},
            {typeof (short?), new Column(DbType.Int16)},
            {typeof (ushort?), new Column(DbType.UInt16)},
            {typeof (int?), new Column(DbType.Int32)},
            {typeof (uint?), new Column(DbType.UInt32)},
            {typeof (long?), new Column(DbType.Int64)},
            {typeof (ulong?), new Column(DbType.UInt64)},
            {typeof (float?), new Column(DbType.Single)},
            {typeof (double?), new Column(DbType.Double)},
            {typeof (decimal?), new Column(DbType.Decimal)},
            {typeof (bool?), new Column(DbType.Boolean)},
            {typeof (char?), new Column(DbType.StringFixedLength)},
            {typeof (Guid?), new Column(DbType.Guid)},
            {typeof (DateTime?), new Column(DbType.DateTime)},
            {typeof (DateTimeOffset?), new Column(DbType.DateTimeOffset)},
            {typeof (TimeSpan?), new Column(DbType.Time)},
            {typeof (Object), new Column(DbType.Object)}
        };

        readonly Expression<Func<TEntity, TMember>> member;
        readonly Func<TEntity, TMember> getter;

        public ProjectionColumn(Expression<Func<TEntity, TMember>> member)
        {
            this.member = member;
            getter = member.Compile();

            var expression = member.ToString();
            Name = string.Join("", expression.Split('.').Skip(1));
            Type = typeof(TMember);

            var type = (Type.IsEnum) ? Type.BaseType : Type;
            Column = typeToColumn[type];
        }

        public string Name { get; set; }
        public Type Type { get; set; }
        public Column Column { get; private set; }

        public object GetValue(object document)
        {
            return getter((TEntity) document);
        }

        public object Serialize(object value)
        {
            if (Type.IsEnum)
                return value.ToString();

            return value;
        }

        public object SetValue(object value)
        {
            throw new NotImplementedException();
        }
    }
}