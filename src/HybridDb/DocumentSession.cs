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
            var design = store.Configuration.GetDesignFor<T>();
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
            
            var design = configuration.GetDesignFor<T>();

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
            var id = design.GetKey(entity);
            entities.Remove(id);
        }

        public Guid? GetEtagFor(object entity)
        {
            var design = store.Configuration.GetDesignFor(entity.GetType());
            var id = design.GetKey(entity);

            ManagedEntity managedEntity;
            if (!entities.TryGetValue(id, out managedEntity))
                return null;

            return managedEntity.Etag;
        }

        public Dictionary<string, List<string>> GetMetadataFor(object entity)
        {
            var design = store.Configuration.GetDesignFor(entity.GetType());
            var id = design.GetKey(entity);

            ManagedEntity managedEntity;
            if (!entities.TryGetValue(id, out managedEntity))
                return null;

            return managedEntity.Metadata;
        }

        public void SetMetadataFor(object entity, Dictionary<string, List<string>> metadata)
        {
            var design = store.Configuration.GetDesignFor(entity.GetType());
            var id = design.GetKey(entity);

            ManagedEntity managedEntity;
            if (!entities.TryGetValue(id, out managedEntity))
                return;

            managedEntity.Metadata = metadata;
        }

        public void Store(string key, object entity)
        {
            if (entities.ContainsKey(key))
                return;

            entities.Add(key, new ManagedEntity
            {
                Key = key,
                Entity = entity,
                State = EntityState.Transient,
            });
        }

        public void Store(object entity)
        {
            var design = store.Configuration.GetDesignFor(entity.GetType());
            var key = design.GetKey(entity);

            if (entities.ContainsKey(key))
                return;

            entities.Add(key, new ManagedEntity
            {
                Key = key,
                Entity = entity,
                State = EntityState.Transient,
            });
        }

        public void Delete(object entity)
        {
            var design = store.Configuration.GetDesignFor(entity.GetType());
            var key = design.GetKey(entity);

            ManagedEntity managedEntity;
            if (!entities.TryGetValue(key, out managedEntity))
                return;

            if (managedEntity.State == EntityState.Transient)
            {
                entities.Remove(key);
            }
            else
            {
                entities[key].State = EntityState.Deleted;
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
                var design = store.Configuration.GetDesignFor(managedEntity.Entity.GetType());
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

        bool SafeSequenceEqual<T>(IEnumerable<T> first, IEnumerable<T> second)
        {
            if (Equals(first, second))
                return true;

            if (first == null || second == null)
                return false;

            return first.SequenceEqual(second);
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
                throw new InvalidOperationException(string.Format("Document with id {0} exists, but is not assignable to the given type {1}.", key, design.DocumentType.Name));
            }

            ManagedEntity managedEntity;
            if (entities.TryGetValue(key, out managedEntity))
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

            entities.Add(key, managedEntity);
            return entity;
        }

        public void Clear()
        {
            entities.Clear();
        }

        public bool IsLoaded(string key)
        {
            return entities.ContainsKey(key);
        }

        public IDocumentStore DocumentStore => store;
    }
}