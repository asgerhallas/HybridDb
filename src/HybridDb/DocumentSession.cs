using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq;
using HybridDb.Migrations;

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

        public IEnumerable<object> ManagedEntities
        {
            get { return entities.Select(x => x.Value.Entity); }
        }

        public T Load<T>(Guid key) where T : class
        {
            ManagedEntity managedEntity;
            if (entities.TryGetValue(key, out managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                    ? managedEntity.Entity as T
                    : null;
            }

            var design = store.Configuration.TryGetDesignFor<T>();
            if (design == null)
            {
                throw new InvalidOperationException(string.Format("No design registered for document of type {0}", typeof (T)));
            }
            
            var row = store.Get(design.Table, key);
            
            if (row == null) return null;

            return (T)ConvertToEntityAndPutUnderManagement(design, row);
        }

        public IQueryable<T> Query<T>() where T : class
        {
            var configuration = store.Configuration;
            
            var design = configuration.TryGetDesignFor<T>();
            if (design == null)
            {
                throw new InvalidOperationException(string.Format("No design registered for type {0}", typeof(T)));
            }

            var discriminators = design.DecendentsAndSelf.Keys.ToArray();

            var query = new Query<T>(new QueryProvider<T>(this, design))
                .Where(x => x.Column<string>("Discriminator").In(discriminators));

            return query;
        }

        public void Defer(DatabaseCommand command)
        {
            deferredCommands.Add(command);
        }

        public void Evict(object entity)
        {
            var design = store.Configuration.GetDesignFor(entity.GetType());
            var id = design.GetId(entity);
            entities.Remove(id);
        }

        public Guid? GetEtagFor(object entity)
        {
            var design = store.Configuration.GetDesignFor(entity.GetType());
            var id = design.GetId(entity);

            ManagedEntity managedEntity;
            if (!entities.TryGetValue(id, out managedEntity))
                return null;

            return managedEntity.Etag;
        }

        public void Store(object entity)
        {
            var configuration = store.Configuration;
            var type = entity.GetType();
            var design = configuration.GetDesignFor(type);
            var id = design.GetId(entity);

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
            var design = store.Configuration.GetDesignFor(entity.GetType());
            var id = design.GetId(entity);

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
                var projections = design.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(managedEntity.Entity));
                projections.Add(design.Table.VersionColumn, store.Configuration.CurrentVersion);

                var document = (byte[])projections[design.Table.DocumentColumn];

                switch (managedEntity.State)
                {
                    case EntityState.Transient:
                        commands.Add(Tuple.Create(managedEntity, document, (DatabaseCommand)new InsertCommand(design.Table, id, projections)));
                        break;
                    case EntityState.Loaded:
                        if (!managedEntity.Document.SequenceEqual(document))
                        {
                            commands.Add(Tuple.Create(managedEntity, document, (DatabaseCommand) new UpdateCommand(design.Table, id, managedEntity.Etag, projections, lastWriteWins)));
                        }
                        break;
                    case EntityState.Deleted:
                        commands.Add(Tuple.Create(managedEntity, document, (DatabaseCommand)new DeleteCommand(design.Table, id, managedEntity.Etag, lastWriteWins)));
                        break;
                }
            }

            if (commands.Count + deferredCommands.Count == 0)
                return;

            var etag = store.Execute(commands.Select(x => x.Item3).Concat(deferredCommands));

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

        internal object ConvertToEntityAndPutUnderManagement(DocumentDesign design, IDictionary<string, object> row)
        {
            var table = design.Table;
            var id = (Guid)row[table.IdColumn];
            var discriminator = ((string)row[table.DiscriminatorColumn]).Trim();

            DocumentDesign concreteDesign;
            if (!design.DecendentsAndSelf.TryGetValue(discriminator, out concreteDesign))
            {
                throw new InvalidOperationException(string.Format("Document with id {0} exists, but is not assignable to the given type {1}.", id, design.DocumentType.Name));
            }

            ManagedEntity managedEntity;
            if (entities.TryGetValue(id, out managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                    ? managedEntity.Entity
                    : null;
            }

            var document = (byte[])row[table.DocumentColumn];
            var entity = DeserializeAndMigrate(store, concreteDesign, row);

            managedEntity = new ManagedEntity
            {
                Key = id,
                Entity = entity,
                Etag = (Guid) row[table.EtagColumn],
                State = EntityState.Loaded,
                Document = document
            };

            entities.Add(id, managedEntity);
            return entity;
        }

        internal static object DeserializeAndMigrate(IDocumentStore store, DocumentDesign design, IDictionary<string, object> row)
        {
            var table = design.Table;
            
            var document = (byte[])row[table.DocumentColumn];
            
            var currentDocumentVersion = (int)row[table.VersionColumn];
            if (store.Configuration.CurrentVersion <= currentDocumentVersion)
            {
                return store.Configuration.Serializer.Deserialize(document, design.DocumentType);
            }
            
            foreach (var migration in store.Configuration.Migrations.Where(x => x.Version > currentDocumentVersion))
            {
                var commands = migration.MigrateDocument();
                foreach (var command in commands.OfType<ChangeDocument>().Where(x => x.ForType(design.DocumentType)))
                {
                    document = command.Execute(store.Configuration.Serializer, document);
                }
            }

            return store.Configuration.Serializer.Deserialize(document, design.DocumentType);
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