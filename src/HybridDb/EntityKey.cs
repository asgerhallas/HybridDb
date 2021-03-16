using System;
using HybridDb.Config;

namespace HybridDb
{
    /// <summary>
    /// Represents a global identification of a document consisting of both type and ID (case insensitive)
    /// </summary>
    public class EntityKey
    {
        public EntityKey(Table table, string id)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public Table Table { get; }
        public string Id { get; }

        protected bool Equals(EntityKey other) =>
            Equals(Table, other.Table) &&
            string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);  

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            return Equals((EntityKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Table.GetHashCode() * 397) ^ Id.ToLowerInvariant().GetHashCode();
            }
        }
    }
}