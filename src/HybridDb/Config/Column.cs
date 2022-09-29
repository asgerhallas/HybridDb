using System;
using System.Data;

namespace HybridDb.Config
{
    public class Column
    {
        public Column(string name, Type type, int? length = null, bool? nullable = null, object defaultValue = null, bool isPrimaryKey = false) 
            : this(name, null, type, length, nullable, defaultValue, isPrimaryKey) { }

        public Column(string name, SqlDbType? dbType, Type type, int? length = null, bool? nullable = null, object defaultValue = null, bool isPrimaryKey = false)
        {
            Type = System.Nullable.GetUnderlyingType(type) ?? type;

            if (defaultValue != null)
            {
                if (type == typeof(byte[]))
                    throw new ArgumentException("Byte array column can not have default value.");

                if (Type != defaultValue.GetType())
                    throw new ArgumentException($"Default value ({type.Name}) must be of same type as column ({defaultValue.GetType().Name}).");
            }

            Name = name;
            Length = length;
            IsPrimaryKey = isPrimaryKey;

            var canBeNull = type.CanBeNull() && !IsPrimaryKey;

            Nullable = nullable is null
                ? canBeNull 
                : nullable.Value && canBeNull;
             
            if (Type.IsEnum)
            {
                Type = typeof(Enum);
            }

            if (dbType == null)
            {
                var sqlTypeMapping = SqlTypeMap.ForNetType(Type);

                if (sqlTypeMapping == null)
                    throw new ArgumentException("Can only project .NET simple types, Guid, DateTime, DateTimeOffset, TimeSpan and byte[].");

                DbType = sqlTypeMapping.DbType;
            }
            else
            {
                DbType = dbType.Value;
            }

            if (!Nullable && defaultValue == null && dbType != SqlDbType.Timestamp)
            {
                DefaultValue = typeof (string).IsAssignableFrom(Type) ? "" : Activator.CreateInstance(type);
            }
            else
            {
                DefaultValue = defaultValue;
            }
        }

        public string Name { get; }
        public Type Type { get; }        
        public SqlDbType DbType { get; protected set; }        
        public int? Length { get; }
        public bool Nullable { get; set; }
        public object DefaultValue { get; }
        public bool IsPrimaryKey { get; }

        public override string ToString() => $"{Name} ({Type})";

        protected bool Equals(Column other) =>
            string.Equals(Name, other.Name, StringComparison.InvariantCultureIgnoreCase) &&
            Type == other.Type &&
            Length == other.Length &&
            Nullable == other.Nullable &&
            IsPrimaryKey == other.IsPrimaryKey &&
            Equals(DefaultValue, other.DefaultValue);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Column) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.ToLowerInvariant().GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Length.GetHashCode();
                hashCode = (hashCode*397) ^ Nullable.GetHashCode();
                hashCode = (hashCode*397) ^ (DefaultValue != null ? DefaultValue.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ IsPrimaryKey.GetHashCode();
                return hashCode;
            }
        }

        public static implicit operator string(Column self) => self.Name;
    }

    public class Column<T> : Column
    {
        public Column(string name, int? length = null, bool? nullable = null, object defaultValue = null, bool isPrimaryKey = false) : base(name, typeof(T), length, nullable, defaultValue, isPrimaryKey) { }
        public Column(string name, SqlDbType? dbType, int? length = null, bool? nullable = null, object defaultValue = null, bool isPrimaryKey = false) : base(name, dbType, typeof(T), length, nullable, defaultValue, isPrimaryKey) { }
    }
}