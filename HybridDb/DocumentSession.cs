using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Commands;
using HybridDb.Linq;
using HybridDb.Schema;

namespace HybridDb
{
    public class DocumentSession : IDocumentSession, IAdvancedDocumentSessionCommands
    {
        readonly Dictionary<Guid, ManagedEntity> entities;
        readonly IDocumentStore store;
        readonly List<DatabaseCommand> deferredCommands;

        public DocumentSession(IDocumentStore store)
        {
            deferredCommands = new List<DatabaseCommand>();
            entities = new Dictionary<Guid, ManagedEntity>();
            this.store = store;
        }

        public IAdvancedDocumentSessionCommands Advanced
        {
            get { return this; }
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

            return ConvertToEntityAndPutUnderManagement<T>(table, row);
        }

        public IEnumerable<T> Query<T>(string where, object parameters) where T : class
        {
            var table = store.Configuration.GetTableFor<T>();
            QueryStats stats;
            var rows = store.Query(table, out stats, where: @where, skip: 0, take: 0, orderby: "", parameters: parameters);

            return rows.Select(row => ConvertToEntityAndPutUnderManagement<T>(table, row))
                       .Where(entity => entity != null);
        }

        public IEnumerable<TProjection> Query<T, TProjection>(string where, object parameters) where T : class
        {
            var table = store.Configuration.GetTableFor<T>();
            QueryStats stats;
            var rows = store.Query<TProjection>(table, out stats, where: @where, skip: 0, take: 0, orderby: "", parameters: parameters);
            return rows;
        }

        public IQueryable<T> Query<T>() where T : class
        {
            return new Query<T>(new QueryProvider<T>(this));
        }

        public void Defer(DatabaseCommand command)
        {
            deferredCommands.Add(command);
        }

        public void Evict(object entity)
        {
            var table = store.Configuration.GetTableFor(entity.GetType());
            var id = (Guid) table.IdColumn.GetValue(entity);
            entities.Remove(id);
        }

        public Guid? GetEtagFor(object entity)
        {
            var table = store.Configuration.GetTableFor(entity.GetType());
            var id = (Guid) table.IdColumn.GetValue(entity);

            ManagedEntity managedEntity;
            if (!entities.TryGetValue(id, out managedEntity))
                return null;

            return managedEntity.Etag;
        }

        public void Store(object entity)
        {
            var table = store.Configuration.GetTableFor(entity.GetType());
            var id = (Guid) table.IdColumn.GetValue(entity);
            if (entities.ContainsKey(id))
                return;

            entities.Add(id, new ManagedEntity
            {
                Key = id,
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
            SaveChangesInternal(false);
        }

        public void SaveChangesLastWriterWins()
        {
            SaveChangesInternal(true);
        }

        void SaveChangesInternal(bool lastWriteWins)
        {
            var serializer = store.Configuration.Serializer;

            var commands = new Dictionary<ManagedEntity, DatabaseCommand>();
            foreach (var managedEntity in entities.Values)
            {
                var id = managedEntity.Key;
                var table = store.Configuration.GetTableFor(managedEntity.Entity.GetType());
                var projections = table.Columns.OfType<ProjectionColumn>().ToDictionary(x => x.Name, x => x.GetValue(managedEntity.Entity));
                var document = serializer.Serialize(managedEntity.Entity);

                switch (managedEntity.State)
                {
                    case EntityState.Transient:
                        commands.Add(managedEntity, new InsertCommand(table, id, document, projections));
                        break;
                    case EntityState.Loaded:
                        if (!managedEntity.Document.SequenceEqual(document))
                            commands.Add(managedEntity, new UpdateCommand(table, id, managedEntity.Etag, document, projections, lastWriteWins));
                        break;
                    case EntityState.Deleted:
                        commands.Add(managedEntity, new DeleteCommand(table, id, managedEntity.Etag, lastWriteWins));
                        break;
                }
            }

            if (commands.Count + deferredCommands.Count == 0)
                return;

            var etag = store.Execute(commands.Values.Concat(deferredCommands).ToArray());

            foreach (var change in commands)
            {
                var managedEntity = change.Key;
                var command = change.Value;

                var insertCommand = command as InsertCommand;
                if (insertCommand != null)
                {
                    managedEntity.State = EntityState.Loaded;
                    managedEntity.Etag = etag;
                    managedEntity.Document = insertCommand.Document;
                    continue;
                }

                var updateCommand = command as UpdateCommand;
                if (updateCommand != null)
                {
                    managedEntity.Etag = etag;
                    managedEntity.Document = updateCommand.Document;
                    continue;
                }

                if (command is DeleteCommand)
                {
                    entities.Remove(managedEntity.Key);
                }
            }
        }

        public void Dispose() {}

        internal T ConvertToEntityAndPutUnderManagement<T>(Table table, IDictionary<Column, object> row)
        {
            var id = (Guid) row[table.IdColumn];

            ManagedEntity managedEntity;
            if (entities.TryGetValue(id, out managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                           ? (T) managedEntity.Entity
                           : default(T);
            }

            var document = (byte[]) row[table.DocumentColumn];
            var entity = store.Configuration.Serializer.Deserialize(document, typeof (T));

            managedEntity = new ManagedEntity
            {
                Key = id,
                Entity = entity,
                Etag = (Guid) row[table.EtagColumn],
                State = EntityState.Loaded,
                Document = document
            };

            entities.Add(id, managedEntity);
            return (T) entity;
        }


        public void Clear()
        {
            entities.Clear();
        }

        public bool IsLoaded(Guid id)
        {
            return entities.ContainsKey(id);
        }

        public IDocumentStore DocumentStore
        {
            get { return store; }
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
            public Guid Key { get; set; }
            public Guid Etag { get; set; }
            public EntityState State { get; set; }
            public byte[] Document { get; set; }
        }
    }
}