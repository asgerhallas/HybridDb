using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Bson;

namespace HybridDb
{
    public class DocumentSession : IDocumentSession 
    {
        readonly IDocumentStore store;
        readonly Dictionary<Type, ITable> entityConfigurations;
        readonly Dictionary<Guid, ManagedEntity> entities;

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

        public DocumentSession(IDocumentStore store, Dictionary<Type, ITable> entityConfigurations)
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
                var id = ((dynamic) entity.Entity).Id;
                var table = entityConfigurations[entity.Entity.GetType()];
                var projections = table.Columns.OfType<IProjectionColumn>().ToDictionary(x => x.Name, x => x.GetValue(entity.Entity));
                switch (entity.State)
                {
                    case EntityState.Transient:
                        store.Insert(id, projections, GetValue(entity.Entity));
                        break;
                    case EntityState.Loaded:
                        store.Update(id, null, projections, GetValue(entity.Entity));
                        break;
                    case EntityState.Deleted:
                        throw new NotImplementedException();
                        break;
                }
            }
        }

        public byte[] GetValue(object document)
        {
            using (var outStream = new MemoryStream())
            using (var bsonWriter = new BsonWriter(outStream))
            {
                serializer.Serialize(bsonWriter, document);
                return outStream.ToArray();
            }
        }

        public object SetValue(object value)
        {
            using (var inStream = new MemoryStream((byte[])value))
            using (var bsonReader = new BsonReader(inStream))
            {
                return serializer.Deserialize(bsonReader, documentType);
            }
        }


        public void Dispose()
        {
            
        }
    }
}