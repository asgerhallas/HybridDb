using System;

namespace HybridDb
{
    /// <summary>
    /// Represents a global identification of a document consisting of both type and ID
    /// </summary>
    class EntityKey
    {
        public EntityKey(Type type, string id)
        {
            Type = type;
            Id = id;
        }

        public Type Type { get; private set; }
        public string Id { get; private set; }

        protected bool Equals(EntityKey other)
        {
            return Equals(Type, other.Type) && string.Equals(Id, other.Id);
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
                return ((Type != null ? Type.GetHashCode() : 0)*397) ^ (Id != null ? Id.GetHashCode() : 0);
            }
        }
    }
}