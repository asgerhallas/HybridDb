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
        readonly Dictionary<string, ManagedEntity> entities;
        readonly IDocumentStore store;
        readonly List<DatabaseCommand> deferredCommands;
        readonly DocumentMigrator migrator;
        bool saving = false;

        public DocumentSession(IDocumentStore store)
        {
            deferredCommands = new List<DatabaseCommand>();
            entities = new Dictionary<string, ManagedEntity>();
            migrator = new DocumentMigrator(store.Configuration);

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

        public T Load<T>(string key) where T : class
        {
            var design = store.Configuration.TryGetDesignFor<T>();
            if (design == null)
            {
                throw new InvalidOperationException(string.Format("No design registered for document of type {0}", typeof(T)));
            }

            return Load(design, key) as T;
        }

        public object Load(DocumentDesign design, string key)
        {
            ManagedEntity managedEntity;
            if (entities.TryGetValue(key, out managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                    ? managedEntity.Entity
                    : null;
            }
            
            var row = store.Get(design.Table, key);
            
            if (row == null) return null;

            return ConvertToEntityAndPutUnderManagement(design, row);
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
            SaveChangesInternal(lastWriteWins: false, force: false);
        }

        public void SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument)
        {
            SaveChangesInternal(lastWriteWins, forceWriteUnchangedDocument);
        }

        void SaveChangesInternal(bool lastWriteWins, bool force)
        {
            if (saving)
            {
                throw new InvalidOperationException("Session is not in a valid state. Please dispose it and open a new one.");
            }

            saving = true;

            var commands = new Dictionary<ManagedEntity, DatabaseCommand>();
            foreach (var managedEntity in entities.Values.ToList())
            {
                var id = managedEntity.Key;
                var design = store.Configuration.GetDesignFor(managedEntity.Entity.GetType());
                var projections = design.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(managedEntity.Entity));

                var version = (int)projections[design.Table.VersionColumn];
                var document = (byte[])projections[design.Table.DocumentColumn];

                switch (managedEntity.State)
                {
                    case EntityState.Transient:
                        commands.Add(managedEntity, new InsertCommand(design.Table, id, projections));
                        managedEntity.State = EntityState.Loaded;
                        managedEntity.Version = version;
                        managedEntity.Document = document;
                        break;
                    case EntityState.Loaded:
                        if (!force && managedEntity.Document.SequenceEqual(document)) 
                            break;
                        
                        commands.Add(managedEntity, new BackupCommand(
                            new UpdateCommand(design.Table, id, managedEntity.Etag, projections, lastWriteWins),
                            store.Configuration.BackupWriter, design, id, managedEntity.Version, managedEntity.Document));
                        
                        managedEntity.Version = version;
                        managedEntity.Document = document;
                        break;
                    case EntityState.Deleted:
                        commands.Add(managedEntity, new DeleteCommand(design.Table, id, managedEntity.Etag, lastWriteWins));
                        entities.Remove(managedEntity.Key);
                        break;
                }
            }

            var etag = store.Execute(commands.Select(x => x.Value).Concat(deferredCommands));

            foreach (var managedEntity in commands.Keys)
            {
                managedEntity.Etag = etag;
            }

            saving = false;
        }

        public void Dispose() {}

        internal object ConvertToEntityAndPutUnderManagement(DocumentDesign design, IDictionary<string, object> row)
        {
            var table = design.Table;
            var id = (string)row[table.IdColumn];
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
            var currentDocumentVersion = (int) row[table.VersionColumn];
            var entity = migrator.DeserializeAndMigrate(this, concreteDesign, id, document, currentDocumentVersion);

            managedEntity = new ManagedEntity
            {
                Key = id,
                Entity = entity,
                Etag = (Guid) row[table.EtagColumn],
                State = EntityState.Loaded,
                Version = currentDocumentVersion,
                Document = document
            };

            entities.Add(id, managedEntity);
            return entity;
        }

        public void Clear()
        {
            entities.Clear();
        }

        public bool IsLoaded(string id)
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
            public string Key { get; set; }
            public Guid Etag { get; set; }
            public EntityState State { get; set; }
            public int Version { get; set; }
            public byte[] Document { get; set; }
        }
    }
}