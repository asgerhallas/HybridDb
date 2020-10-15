using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Events;
using HybridDb.Events.Commands;
using HybridDb.Linq.Old;
using HybridDb.Migrations.Documents;

namespace HybridDb
{
    public class DocumentSession : IDocumentSession, IAdvancedDocumentSession
    {
        readonly IDocumentStore store;

        readonly Dictionary<EntityKey, ManagedEntity> entities;
        readonly List<(int Generation, EventData<byte[]> Data)> events;
        readonly DocumentTransaction enlistedTx;
        readonly List<DmlCommand> deferredCommands;
        readonly DocumentMigrator migrator;

        bool saving = false;

        internal DocumentSession(IDocumentStore store, DocumentTransaction tx = null)
        {
            deferredCommands = new List<DmlCommand>();
            entities = new Dictionary<EntityKey, ManagedEntity>();
            events = new List<(int Generation, EventData<byte[]> Data)>();
            migrator = new DocumentMigrator(store.Configuration);

            this.store = store;

            enlistedTx = tx;
        }

        public IDocumentStore DocumentStore => store;

        public IAdvancedDocumentSession Advanced => this;
        public IReadOnlyDictionary<EntityKey, ManagedEntity> ManagedEntities => entities;

        public T Load<T>(string key) where T : class => Load(typeof(T), key) as T;

        public object Load(Type type, string key)
        {
            var design = store.Configuration.GetOrCreateDesignFor(type);

            if (entities.TryGetValue(new EntityKey(design.Table, key), out var managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                    ? managedEntity.Entity
                    : null;
            }

            var row = Transactionally(tx => tx.Get(design.Table, key));
            
            if (row == null) return null;

            var concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(design, (string)row[DocumentTable.DiscriminatorColumn]);

            // The discriminator does not map to a type that is assignable to the expected type.
            if (!type.IsAssignableFrom(concreteDesign.DocumentType))
            {
                throw new InvalidOperationException($"Document with id '{key}' exists, but is of type '{concreteDesign.DocumentType}', which is not assignable to '{type}'.");
            }

            return ConvertToEntityAndPutUnderManagement(concreteDesign, row);
        }


        /// <summary>
        /// Query for document of type T and subtypes of T in the table assigned to T.
        /// Note that if a subtype of T is specifically assigned to another table in store configuration,
        /// it will not be included in the result of Query&lt;T&gt;().
        /// </summary>
        public IQueryable<T> Query<T>() where T : class
        {
            var configuration = store.Configuration;

            var design = configuration.GetOrCreateDesignFor(typeof(T));

            var include = design.DecendentsAndSelf.Keys.ToArray();
            var exclude = design.Root.DecendentsAndSelf.Keys.Except(include).ToArray();

            // Include T and known subtybes, exclude known supertypes.
            // Unknown discriminators will be included and filtered result-side
            // but also be added to configuration so they are known on next query.
            var query = new Query<T>(new QueryProvider(this, design)).Where(x =>
                x.Column<string>("Discriminator").In(include)
                || !x.Column<string>("Discriminator").In(exclude));

            return query;
        }

        public void Defer(DmlCommand command)
        {
            deferredCommands.Add(command);
        }

        public void Evict(object entity)
        {
            var managedEntity = TryGetManagedEntity(entity);

            if (managedEntity == null) return;

            entities.Remove(new EntityKey(managedEntity.Table, managedEntity.Key));
        }


        public Guid? GetEtagFor(object entity) => TryGetManagedEntity(entity)?.Etag;

        public Dictionary<string, List<string>> GetMetadataFor(object entity) => TryGetManagedEntity(entity)?.Metadata;

        public void SetMetadataFor(object entity, Dictionary<string, List<string>> metadata)
        {
            var managedEntity = TryGetManagedEntity(entity);
            if (managedEntity == null) return;

            managedEntity.Metadata = metadata;
        }

        public void Store(object entity) => Store(null, entity);
        public void Store(object entity, Guid? etag) => Store(null, entity, etag);
        public void Store(string key, object entity, Guid? etag) => Store(key, entity, etag, EntityState.Loaded);
        public void Store(string key, object entity) => Store(key, entity, null, EntityState.Transient);

        void Store(string key, object entity, Guid? etag, EntityState state)
        {
            if (entity == null) return;

            var design = store.Configuration.GetOrCreateDesignFor(entity.GetType());

            key = key ?? design.GetKey(entity);

            var entityKey = new EntityKey(design.Table, key);

            if (entities.TryGetValue(entityKey, out var managedEntity))
            {
                // Storing a new instance under an existing id, is an error
                if (managedEntity.Entity != entity)
                    throw new HybridDbException($"Attempted to store a different object with id '{key}'.");

                // Storing same instance is a noop
                return;
            }

            entities.Add(entityKey, new ManagedEntity
            {
                Key = key,
                Entity = entity,
                State = state,
                Etag = etag,
                Table = design.Table
            });
        }

        public void Append(int generation, EventData<byte[]> @event)
        {
            events.Add((generation, @event));
        }


        public void Delete(object entity)
        {
            var managedEntity = TryGetManagedEntity(entity);
            if (managedEntity == null) return;

            var entityKey = new EntityKey(managedEntity.Table, managedEntity.Key);

            if (managedEntity.State == EntityState.Transient)
            {
                entities.Remove(entityKey);
            }
            else
            {
                entities[entityKey].State = EntityState.Deleted;
            }
        }

        public Guid SaveChanges() => SaveChanges(null);
        public Guid SaveChanges(DocumentTransaction tx) => SaveChanges(tx, lastWriteWins: false, forceWriteUnchangedDocument: false);
        public Guid SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument) => SaveChanges(null, lastWriteWins, forceWriteUnchangedDocument);

        public Guid SaveChanges(DocumentTransaction tx, bool lastWriteWins, bool forceWriteUnchangedDocument)
        {
            if (saving)
            {
                throw new InvalidOperationException("Session is not in a valid state. Please dispose it and open a new one.");
            }

            saving = true;

            var commands = new Dictionary<ManagedEntity, DmlCommand>();
            foreach (var managedEntity in entities.Values.ToList())
            {
                var key = managedEntity.Key;
                var design = store.Configuration.GetExactDesignFor(managedEntity.Entity.GetType());
                var projections = design.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(managedEntity.Entity, managedEntity.Metadata));

                var configuredVersion = (int)projections[DocumentTable.VersionColumn];
                var document = (string)projections[DocumentTable.DocumentColumn];
                var metadataDocument = (string)projections[DocumentTable.MetadataColumn];

                var expectedEtag = !lastWriteWins ? managedEntity.Etag : null;

                switch (managedEntity.State)
                {
                    case EntityState.Transient:
                        commands.Add(managedEntity, new InsertCommand(design.Table, key, projections));
                        managedEntity.State = EntityState.Loaded;
                        managedEntity.Version = configuredVersion;
                        managedEntity.Document = document;
                        break;
                    case EntityState.Loaded:
                        if (!forceWriteUnchangedDocument && 
                            SafeSequenceEqual(managedEntity.Document, document) && 
                            SafeSequenceEqual(managedEntity.MetadataDocument, metadataDocument))
                            break;

                        commands.Add(managedEntity, new UpdateCommand(design.Table, key, expectedEtag, projections));

                        if (configuredVersion != managedEntity.Version && !string.IsNullOrEmpty(managedEntity.Document))
                        {
                            store.Configuration.BackupWriter.Write($"{design.DocumentType.FullName}_{key}_{managedEntity.Version}.bak", Encoding.UTF8.GetBytes(managedEntity.Document));
                        }

                        managedEntity.Version = configuredVersion;
                        managedEntity.Document = document;

                        break;
                    case EntityState.Deleted:
                        commands.Add(managedEntity, new DeleteCommand(design.Table, key, expectedEtag));
                        entities.Remove(new EntityKey(design.Table, managedEntity.Key));
                        break;
                }
            }

            var commitId = Transactionally(resultingTx =>
            {
                foreach (var command in commands.Select(x => x.Value).Concat(deferredCommands))
                {
                    store.Execute(resultingTx, command);
                }

                if (store.Configuration.EventStore)
                {
                    var eventTable = store.Configuration.Tables.Values.OfType<EventTable>().Single();

                    foreach (var @event in events)
                    {
                        resultingTx.Execute(new AppendEvent(eventTable, @event.Generation, @event.Data));
                    }
                }

                return resultingTx.CommitId;
            }, tx);

            foreach (var managedEntity in commands.Keys)
            {
                managedEntity.Etag = commitId;
            }

            saving = false;

            return commitId;
        }

        public void Dispose() {}

        internal object ConvertToEntityAndPutUnderManagement(DocumentDesign concreteDesign, IDictionary<string, object> row)
        {
            var table = concreteDesign.Table;
            var key = (string)row[DocumentTable.IdColumn];

            if (entities.TryGetValue(new EntityKey(concreteDesign.Table, key), out var managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                    ? managedEntity.Entity
                    : null;
            }

            var document = (string)row[DocumentTable.DocumentColumn];
            var documentVersion = (int) row[DocumentTable.VersionColumn];

            var entity = migrator.DeserializeAndMigrate(this, concreteDesign, row);

            if (entity.GetType() != concreteDesign.DocumentType)
            {
                throw new InvalidOperationException($"Requested a document of type '{concreteDesign.DocumentType}', but got a '{entity.GetType()}'.");
            }

            var metadataDocument = (string)row[DocumentTable.MetadataColumn];
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
                Etag = (Guid) row[DocumentTable.EtagColumn],
                Version = documentVersion,
                State = EntityState.Loaded,
                Table = table
            };

            entities.Add(new EntityKey(concreteDesign.Table, key), managedEntity);
            return entity;
        }

        internal T Transactionally<T>(Func<DocumentTransaction, T> func, DocumentTransaction overrideTx = null)
        {
            if (overrideTx != null)
            {
                if (enlistedTx != null && enlistedTx != overrideTx)
                {
                    throw new InvalidOperationException("Session is already enlisted in another transaction.");
                }

                return func(overrideTx);
            }

            if (enlistedTx != null)
            {
                return func(enlistedTx);
            }
            
            return store.Transactionally(func);
        }

        public void Clear() => entities.Clear();

        public bool TryGetManagedEntity<T>(string key, out T entity)
        {
            if (entities.TryGetValue(new EntityKey(store.Configuration.GetExactDesignFor(typeof(T)).Table, key), out var managedEntity))
            {
                entity = (T)managedEntity.Entity;
                return true;
            }

            entity = default;
            return false;
        }

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