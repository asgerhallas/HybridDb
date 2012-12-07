using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public class DocumentSession : IDocumentSession 
    {
        readonly IDocumentStore store;
        readonly Dictionary<Type, ITableConfiguration> entityConfigurations;
        readonly Dictionary<Guid, ManagedEntity> entities;
        readonly List<object> transientEntities;
        readonly List<object> deletedEntities;

        class ManagedEntity
        {
            public object Entity { get; set; }
            public EntityState State { get; set; }
        }

        enum EntityState
        {
            Transient,
            Loaded,
            Deleted
        }

        public DocumentSession(IDocumentStore store, Dictionary<Type, ITableConfiguration> entityConfigurations)
        {
            entities = new Dictionary<Guid, ManagedEntity>();

            this.store = store;
            this.entityConfigurations = entityConfigurations;
        }

        public T Load<T>(Guid id) where T:class
        {
            ManagedEntity managedEntity;
            if (entities.TryGetValue(id, out managedEntity))
                return (T)managedEntity.Entity;

            var table = entityConfigurations[typeof (T)];
            var values = store.Get(table, id);
            var entity = table.DocumentColumn.SetValue(values[table.DocumentColumn.Name]);

            managedEntity = new ManagedEntity
            {
                Entity = entity,
                State = EntityState.Loaded
            };
            
            entities.Add(id, managedEntity);
            return (T) entity;
        }

        public void Store(object entity)
        {
            var table = entityConfigurations[entity.GetType()];
            var id = (Guid) table.IdColumn.GetValue(entity);
            if (entities.ContainsKey(id))
                return;

            entities.Add(id, new ManagedEntity
            {
                Entity = entity,
                State = EntityState.Transient
            });
        }

        public void Delete(object entity)
        {
            deletedEntities.Add(entity);
        }

        public void SaveChanges()
        {
            foreach (var entity in entities.Values)
            {
                var table = entityConfigurations[entity.Entity.GetType()];
                switch (entity.State)
                {
                    case EntityState.Transient:
                        store.Insert(table, table.Columns.ToDictionary(x => x.Name, x => x.GetValue(entity.Entity)));
                        break;
                    case EntityState.Loaded:
                        store.Update(table, table.Columns.ToDictionary(x => x.Name, x => x.GetValue(entity.Entity)));
                        break;
                    case EntityState.Deleted:
                        throw new NotImplementedException();
                        break;
                }
            }
        }

        public void Dispose()
        {
            
        }
    }
}