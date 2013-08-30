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
                           ? managedEntity.Entity as T
                           : null;
            }

            var table = store.Configuration.GetDesignFor<T>();
            var row = store.Get(table.Table, id);
            if (row == null)
                return null;

            return ConvertToEntityAndPutUnderManagement<T>(table.Table, row);
        }

        public T Load<T, TIndex>(Guid id) where T : class
        {
            ManagedEntity managedEntity;
            if (entities.TryGetValue(id, out managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                           ? managedEntity.Entity as T
                           : null;
            }

            var design = store.Configuration.GetDesignFor<T>();
            
            var row = store.GetByIndex(design.IndexTables.Single(), id);
            if (row == null)
                return null;

            return ConvertToEntityAndPutUnderManagement<T>(design.Table, row);
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
            var table = store.Configuration.GetDesignFor(entity.GetType());
            var id = (Guid) table.Projections[table.Table.IdColumn](entity);
            entities.Remove(id);
        }

        public Guid? GetEtagFor(object entity)
        {
            var table = store.Configuration.GetDesignFor(entity.GetType());
            var id = (Guid)table.Projections[table.Table.IdColumn](entity);

            ManagedEntity managedEntity;
            if (!entities.TryGetValue(id, out managedEntity))
                return null;

            return managedEntity.Etag;
        }

        public void Store(object entity)
        {
            var table = store.Configuration.GetDesignFor(entity.GetType());
            var id = (Guid)table.Projections[table.Table.IdColumn](entity);
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
            var table = store.Configuration.GetDesignFor(entity.GetType());
            var id = (Guid)table.Projections[table.Table.IdColumn](entity);

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
            var commands = new List<Tuple<ManagedEntity, byte[], DatabaseCommand>>();
            foreach (var managedEntity in entities.Values)
            {
                var id = managedEntity.Key;
                var design = store.Configuration.GetDesignFor(managedEntity.Entity.GetType());
                var projections = design.Projections.ToDictionary(x => x.Key.Name, x => x.Value(managedEntity.Entity));
                var document = (byte[])projections[design.Table.DocumentColumn.Name];

                switch (managedEntity.State)
                {
                    case EntityState.Transient:
                        commands.Add(Tuple.Create(managedEntity, document, (DatabaseCommand)new InsertCommand(design.Table, id, projections)));
                        commands.AddRange(design.IndexTables.Select(indexTable => Tuple.Create(managedEntity, document, (DatabaseCommand) new InsertCommand(indexTable, id, projections))));
                        break;
                    case EntityState.Loaded:
                        if (!managedEntity.Document.SequenceEqual(document))
                            commands.Add(Tuple.Create(managedEntity, document, (DatabaseCommand)new UpdateCommand(design.Table, id, managedEntity.Etag, projections, lastWriteWins)));
                        break;
                    case EntityState.Deleted:
                        commands.Add(Tuple.Create(managedEntity, document, (DatabaseCommand)new DeleteCommand(design.Table, id, managedEntity.Etag, lastWriteWins)));
                        break;
                }
            }

            if (commands.Count + deferredCommands.Count == 0)
                return;

            var etag = store.Execute(commands.Select(x => x.Item3).Concat(deferredCommands).ToArray());

            foreach (var change in commands)
            {
                var managedEntity = change.Item1;
                var document = change.Item2;
                var command = change.Item3;

                var insertCommand = command as InsertCommand;
                if (insertCommand != null)
                {
                    managedEntity.State = EntityState.Loaded;
                    managedEntity.Etag = etag;
                    managedEntity.Document = document;
                    continue;
                }

                var updateCommand = command as UpdateCommand;
                if (updateCommand != null)
                {
                    managedEntity.Etag = etag;
                    managedEntity.Document = document;
                    continue;
                }

                if (command is DeleteCommand)
                {
                    entities.Remove(managedEntity.Key);
                }
            }
        }

        public void Dispose() {}

        internal T ConvertToEntityAndPutUnderManagement<T>(DocumentTable table, IDictionary<Column, object> row)
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