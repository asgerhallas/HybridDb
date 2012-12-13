using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public class DocumentSession : IDocumentSession
    {
        readonly IDocumentStore store;
        readonly Dictionary<Guid, ManagedEntity> entities;

        class ManagedEntity
        {
            public object Entity { get; set; }
            public Guid Etag { get; set; }
            public EntityState State { get; set; }
        }

        enum EntityState
        {
            Transient,
            Loaded,
            Deleted
        }

        public DocumentSession(IDocumentStore store)
        {
            entities = new Dictionary<Guid, ManagedEntity>();

            this.store = store;
        }

        public T Load<T>(Guid id) where T:class
        {
            ManagedEntity managedEntity;
            if (entities.TryGetValue(id, out managedEntity))
                return (T)managedEntity.Entity;

            var table = store.Configuration.GetTableFor<T>();
            var document = store.Get(table, id);
            var entity = store.Configuration.CreateSerializer().Deserialize<T>(document.Data);

            managedEntity = new ManagedEntity
            {
                Entity = entity,
                State = EntityState.Loaded
            };
            
            entities.Add(id, managedEntity);
            return entity;
        }

        public void Store(object entity)
        {
            var table = store.Configuration.GetTableFor(entity.GetType());
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
            var table = store.Configuration.GetTableFor(entity.GetType());
            var id = (Guid)table.IdColumn.GetValue(entity);
            if (!entities.ContainsKey(id))
                return;

            entities[id].State = EntityState.Deleted;
        }

        public void SaveChanges()
        {
            var serializer = store.Configuration.CreateSerializer();
            foreach (var entity in entities.Values)
            {
                var id = ((dynamic) entity.Entity).Id;
                var table = store.Configuration.GetTableFor(entity.GetType());
                var projections = table.Columns.OfType<IProjectionColumn>().ToDictionary(x => x.Name, x => x.GetValue(entity.Entity));
                var document = serializer.Serialize(entity);
                switch (entity.State)
                {
                    case EntityState.Transient:
                        store.Insert(table, id, document, projections);
                        break;
                    case EntityState.Loaded:
                        store.Update(table, id, entity.Etag, document, projections);
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