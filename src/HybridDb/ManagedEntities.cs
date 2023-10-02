using System;
using System.Collections;
using System.Collections.Generic;

namespace HybridDb
{
    public class ManagedEntities : IReadOnlyDictionary<EntityKey, ManagedEntity>
    {
        readonly DocumentSession session;
        readonly Dictionary<EntityKey, ManagedEntity> entitiesByKey = new();
        readonly Dictionary<object, ManagedEntity> entitiesByInstance = new();

        public ManagedEntities(DocumentSession session)
        {
            this.session = session;
        }

        public void Add(ManagedEntity managedEntity)
        {
            if (managedEntity == null) throw new ArgumentNullException(nameof(managedEntity));

            entitiesByKey.Add(managedEntity.EntityKey, managedEntity);
            entitiesByInstance.Add(managedEntity.Entity, managedEntity);

            session.DocumentStore.Configuration.Notify(new AddedToSession(session, managedEntity));
        }

        public void Clear()
        {
            foreach (var managedEntity in entitiesByKey.Values)
            {
                session.DocumentStore.Configuration.Notify(new RemovedFromSession(session, managedEntity));
            }

            entitiesByKey.Clear();
            entitiesByInstance.Clear();
        }

        public bool Remove(EntityKey key)
        {
            if (entitiesByKey.TryGetValue(key, out var managedEntity) &&
               entitiesByInstance.Remove(managedEntity.Entity) &&
               entitiesByKey.Remove(key))
            {
                session.DocumentStore.Configuration.Notify(new RemovedFromSession(session, managedEntity));
                return true;
            }

            return false;
        }

        internal void CopyTo(ManagedEntities entities)
        {
            foreach (var entityByKey in entitiesByKey)
            {
                entities.entitiesByKey.Add(entityByKey.Key, entityByKey.Value);
            }

            foreach (var entityByInstance in entitiesByInstance)
            {
                entities.entitiesByInstance.Add(entityByInstance.Key, entityByInstance.Value);
            }
        }

        public bool ContainsKey(EntityKey key) => entitiesByKey.ContainsKey(key);
        public bool TryGetValue(EntityKey key, out ManagedEntity managedEntity) => entitiesByKey.TryGetValue(key, out managedEntity);
        public bool TryGetValue(object entity, out ManagedEntity managedEntity) => entitiesByInstance.TryGetValue(entity, out managedEntity);

        public ManagedEntity this[EntityKey key] => entitiesByKey[key];

        public IEnumerable<EntityKey> Keys => entitiesByKey.Keys;
        public IEnumerable<ManagedEntity> Values => entitiesByKey.Values;
        public int Count => entitiesByKey.Count;

        public IEnumerator<KeyValuePair<EntityKey, ManagedEntity>> GetEnumerator() => entitiesByKey.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}