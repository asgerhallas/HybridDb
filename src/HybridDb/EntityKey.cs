using System;
using HybridDb.Config;

namespace HybridDb
{
    /// <summary>
    /// Represents a global identification of a document consisting of both type and ID
    /// </summary>
    class EntityKey
    {
        public EntityKey(Table table, string id)
        {
            Table = table;
            Id = id;
        }

        public Table Table { get; private set; }
        public string Id { get; private set; }

        protected bool Equals(EntityKey other)
        {
            return Equals(Table.Name, other.Table.Name) && string.Equals(Id, other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EntityKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Table != null ? Table.GetHashCode() : 0)*397) ^ (Id != null ? Id.GetHashCode() : 0);
            }
        }
    }
}