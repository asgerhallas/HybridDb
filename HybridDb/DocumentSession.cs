using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public class DocumentSession : IDocumentSession
    {
        readonly AdvancedDocumentSessionCommands advanced;
        readonly Dictionary<Guid, ManagedEntity> entities;
        readonly IDocumentStore store;

        public DocumentSession(IDocumentStore store)
        {
            entities = new Dictionary<Guid, ManagedEntity>();
            this.store = store;

            advanced = new AdvancedDocumentSessionCommands(this);
        }

        public IAdvancedDocumentSessionCommands Advanced
        {
            get { return advanced; }
        }

        public T Load<T>(Guid id) where T : class
        {
            ManagedEntity managedEntity;
            if (entities.TryGetValue(id, out managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                           ? (T) managedEntity.Entity
                           : null;
            }

            var table = store.Configuration.GetTableFor<T>();
            var row = store.Get(table, id);
            if (row == null)
                return null;

            var document = (byte[]) row[table.DocumentColumn];
            var entity = store.Configuration.CreateSerializer().Deserialize<T>(document);

            managedEntity = new ManagedEntity
            {
                Entity = entity,
                Etag = (Guid) row[table.EtagColumn],
                State = EntityState.Loaded,
                Document = document
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
            var id = (Guid) table.IdColumn.GetValue(entity);

            ManagedEntity managedEntity;
            if (!entities.TryGetValue(id, out managedEntity))
                return;

            if (managedEntity.State == EntityState.Transient)
            {
                entities.Remove(id);
            }
            else
            {
                entities[id].State = EntityState.Deleted;
            }
        }

        public void SaveChanges()
        {
            var serializer = store.Configuration.CreateSerializer();

            var commands = new List<DatabaseCommand>();
            foreach (var managedEntity in entities.Values)
            {
                var id = ((dynamic) managedEntity.Entity).Id;
                var table = store.Configuration.GetTableFor(managedEntity.Entity.GetType());
                var projections = table.Columns.OfType<IProjectionColumn>().ToDictionary(x => x.Name, x => x.GetValue(managedEntity.Entity));
                var document = serializer.Serialize(managedEntity.Entity);
                
                switch (managedEntity.State)
                {
                    case EntityState.Transient:
                        commands.Add(new InsertCommand(table, id, document, projections));
                        break;
                    case EntityState.Loaded:
                        if (!Enumerable.SequenceEqual(managedEntity.Document, document))
                            commands.Add(new UpdateCommand(table, id, managedEntity.Etag, document, projections));
                        break;
                    case EntityState.Deleted:
                        commands.Add(new DeleteCommand(table, id, managedEntity.Etag));
                        break;
                }
            }

            if (commands.Count == 0)
                return;

            var etag = store.Execute(commands.ToArray());

            foreach (var managedEntity in entities.ToList())
            {
                switch (managedEntity.Value.State)
                {
                    case EntityState.Transient:
                        managedEntity.Value.State = EntityState.Loaded;
                        managedEntity.Value.Document = doc
                        managedEntity.Value.Etag = etag;
                        break;
                    case EntityState.Loaded:
                        managedEntity.Value.Etag = etag;
                        break;
                    case EntityState.Deleted:
                        entities.Remove(managedEntity.Key);
                        break;
                }
            }
        }

        public void Dispose() {}

        class AdvancedDocumentSessionCommands : IAdvancedDocumentSessionCommands
        {
            readonly DocumentSession session;

            public AdvancedDocumentSessionCommands(DocumentSession session)
            {
                this.session = session;
            }

            public void Clear()
            {
                session.entities.Clear();
            }

            public bool IsLoaded(Guid id)
            {
                return session.entities.ContainsKey(id);
            }
        }

        enum EntityState
        {
            Transient,
            Loaded,
            Deleted
        }

        class ManagedEntity
        {
            public object Entity { get; set; }
            public Guid Etag { get; set; }
            public EntityState State { get; set; }
            public byte[] Document { get; set; }
        }
    }
}