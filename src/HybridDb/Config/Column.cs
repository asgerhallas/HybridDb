using System;

namespace HybridDb.Config
{
    public class Column
    {
        public Column(string name, Type type, SqlColumn sqlColumn)
        {
            Name = name;
            Type = type;
            SqlColumn = sqlColumn;
        }

        public Column(string name, Type type)
            : this(name, type, type != null ? new SqlColumn(type) : new SqlColumn())
        {}

        public string Name { get; protected set; }
        public Type Type { get; protected set; }
        
        public int? Length { get; set; }
        public bool Nullable { get; set; }
        public object DefaultValue { get; set; }
        public bool IsPrimaryKey { get; set; }

        public SqlColumn SqlColumn { get; protected set; }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, SqlColumn.Type);
        }

        protected bool Equals(Column other)
        {
            return Name == other.Name &&
                   Type == other.Type &&
                   Length == other.Length &&
                   Nullable == other.Nullable &&
                   IsPrimaryKey == other.IsPrimaryKey &&
                   Equals(DefaultValue, other.DefaultValue);
        }

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
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Length.GetHashCode();
                hashCode = (hashCode*397) ^ Nullable.GetHashCode();
                hashCode = (hashCode*397) ^ (DefaultValue != null ? DefaultValue.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ IsPrimaryKey.GetHashCode();
                return hashCode;
            }
        }

        public static implicit operator string(Column self)
        {
            return self.Name;
        }
    }
}