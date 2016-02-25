using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq;
using HybridDb.Migrations;

namespace HybridDb
{
    public class DocumentSession : IDocumentSession, IAdvancedDocumentSessionCommands
    {
        readonly Dictionary<EntityKey, ManagedEntity> entities;
        readonly IDocumentStore store;
        readonly List<DatabaseCommand> deferredCommands;
        readonly DocumentMigrator migrator;
        bool saving = false;

        public DocumentSession(IDocumentStore store)
        {
            deferredCommands = new List<DatabaseCommand>();
            entities = new Dictionary<EntityKey, ManagedEntity>();
            migrator = new DocumentMigrator(store.Configuration);

            this.store = store;
        }

        public IAdvancedDocumentSessionCommands Advanced => this;

        public IEnumerable<ManagedEntity> ManagedEntities => entities
            .Select(x => new ManagedEntity
            {
                Key = x.Value.Key,
                Entity = x.Value.Entity,
                Etag = x.Value.Etag,
                State = x.Value.State,
                Version = x.Value.Version,
                Document = x.Value.Document,
            });

        public T Load<T>(string key) where T : class
        {
            return Load(store.Configuration.TryGetLeastSpecificDesignFor(typeof(T)), key) as T;
        }

        public object Load(DocumentDesign design, string key)
        {
            ManagedEntity managedEntity;
            if (entities.TryGetValue(new EntityKey(design.DocumentType, key), out managedEntity))
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
            
            var design = configuration.TryGetLeastSpecificDesignFor(typeof(T));

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
            var cachedInstance = TryGetManagedEntity(entity);

            if (cachedInstance == null) return;

            entities.Remove(new EntityKey(entity.GetType(), cachedInstance.Key));
        }

        public Guid? GetEtagFor(object entity)
        {
            return TryGetManagedEntity(entity)?.Etag;
        }

        public Dictionary<string, List<string>> GetMetadataFor(object entity)
        {
            return TryGetManagedEntity(entity)?.Metadata;
        }

        public void SetMetadataFor(object entity, Dictionary<string, List<string>> metadata)
        {
            var managedEntity = TryGetManagedEntity(entity);
            if (managedEntity == null) return;

            managedEntity.Metadata = metadata;
        }

        public void Store(string key, object entity)
        {
            var design = store.Configuration.TryGetExactDesignFor(entity.GetType()) 
                ?? store.Configuration.CreateDesignFor(entity.GetType());

            key = key ?? design.GetKey(entity);

            var entityKey = new EntityKey(entity.GetType(), key);

            if (entities.ContainsKey(entityKey))
                return;

            entities.Add(entityKey, new ManagedEntity
            {
                Key = key,
                Entity = entity,
                State = EntityState.Transient,
            });
        }

        public void Store(object entity)
        {
            Store(null, entity);
        }

        public void Delete(object entity)
        {
            var managedEntity = TryGetManagedEntity(entity);
            if (managedEntity == null) return;

            var entityKey = new EntityKey(entity.GetType(), managedEntity.Key);

            if (managedEntity.State == EntityState.Transient)
            {
                entities.Remove(entityKey);
            }
            else
            {
                entities[entityKey].State = EntityState.Deleted;
            }
        }

        public void SaveChanges()
        {
            SaveChangesInternal(lastWriteWins: false, forceWriteUnchangedDocument: false);
        }

        public void SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument)
        {
            SaveChangesInternal(lastWriteWins, forceWriteUnchangedDocument);
        }

        void SaveChangesInternal(bool lastWriteWins, bool forceWriteUnchangedDocument)
        {
            if (saving)
            {
                throw new InvalidOperationException("Session is not in a valid state. Please dispose it and open a new one.");
            }

            saving = true;

            var commands = new Dictionary<ManagedEntity, DatabaseCommand>();
            foreach (var managedEntity in entities.Values.ToList())
            {
                var key = managedEntity.Key;
                var design = store.Configuration.TryGetExactDesignFor(managedEntity.Entity.GetType());
                var projections = design.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(managedEntity));

                var version = (int)projections[design.Table.VersionColumn];
                var document = (byte[])projections[design.Table.DocumentColumn];
                var metadataDocument = (byte[])projections[design.Table.MetadataColumn];

                switch (managedEntity.State)
                {
                    case EntityState.Transient:
                        commands.Add(managedEntity, new InsertCommand(design.Table, key, projections));
                        managedEntity.State = EntityState.Loaded;
                        managedEntity.Version = version;
                        managedEntity.Document = document;
                        break;
                    case EntityState.Loaded:
                        if (!forceWriteUnchangedDocument && 
                            SafeSequenceEqual(managedEntity.Document, document) && 
                            SafeSequenceEqual(managedEntity.MetadataDocument, metadataDocument)) 
                            break;
                        
                        commands.Add(managedEntity, new BackupCommand(
                            new UpdateCommand(design.Table, key, managedEntity.Etag, projections, lastWriteWins),
                            store.Configuration.BackupWriter, design, key, managedEntity.Version, managedEntity.Document));
                        
                        managedEntity.Version = version;
                        managedEntity.Document = document;
                        break;
                    case EntityState.Deleted:
                        commands.Add(managedEntity, new DeleteCommand(design.Table, key, managedEntity.Etag, lastWriteWins));
                        entities.Remove(new EntityKey(design.DocumentType, managedEntity.Key));
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
            var key = (string)row[table.IdColumn];
            var discriminator = ((string)row[table.DiscriminatorColumn]).Trim();

            DocumentDesign concreteDesign;
            if (!design.DecendentsAndSelf.TryGetValue(discriminator, out concreteDesign))
            {
                var type = Type.GetType(discriminator, false, true);

                if (!design.DocumentType.IsAssignableFrom(type))
                {
                    throw new InvalidOperationException($"Document with id {key} exists, but is not assignable to the given type {design.DocumentType.Name}.");
                }

                concreteDesign = store.Configuration.CreateDesignFor(type);
            }

            ManagedEntity managedEntity;
            if (entities.TryGetValue(new EntityKey(design.DocumentType, key), out managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                    ? managedEntity.Entity
                    : null;
            }

            var document = (byte[])row[table.DocumentColumn];
            var currentDocumentVersion = (int) row[table.VersionColumn];
            var entity = migrator.DeserializeAndMigrate(this, concreteDesign, key, document, currentDocumentVersion);

            var metadataDocument = (byte[])row[table.MetadataColumn];
            var metadata = metadataDocument != null
                ? (Dictionary<string, List<string>>) store.Configuration.Serializer.Deserialize(metadataDocument, typeof(Dictionary<string, List<string>>)) 
                : null;

            managedEntity = new ManagedEntity
            {
                Key = key,
                Entity = entity,
                Document = document,
                Metadata = metadata,
                MetadataDocument = metadataDocument,
                Etag = (Guid) row[table.EtagColumn],
                Version = currentDocumentVersion,
                State = EntityState.Loaded,
            };

            entities.Add(new EntityKey(design.DocumentType, key), managedEntity);
            return entity;
        }

        public void Clear()
        {
            entities.Clear();
        }

        public bool IsLoaded<T>(string key)
        {
            return entities.ContainsKey(new EntityKey(typeof(T), key));
        }

        public IDocumentStore DocumentStore => store;

        ManagedEntity TryGetManagedEntity(object entity)
        {
            return entities
                .Select(x => x.Value)
                .SingleOrDefault(x => x.Entity == entity);
        }

        bool SafeSequenceEqual<T>(IEnumerable<T> first, IEnumerable<T> second)
        {
            if (Equals(first, second))
                return true;

            if (first == null || second == null)
                return false;

            return first.SequenceEqual(second);
        }
    }
}