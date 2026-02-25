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
using Microsoft.Data.SqlClient;

namespace HybridDb;

/// <summary>
/// Represents a unit of work and works as a first level cache of loaded documents.
/// </summary>
public class DocumentSession : IDocumentSession, IAdvancedDocumentSession
{
    readonly List<(int Generation, EventData<byte[]> Data)> events;
    readonly List<HybridDbCommand> deferredCommands;
    readonly DocumentMigrator migrator;

    bool saving;

    internal DocumentSession(IDocumentStore store, DocumentMigrator migrator, DocumentTransaction tx = null)
    {
        ManagedEntities = new ManagedEntities(this);
        events = new List<(int Generation, EventData<byte[]> Data)>();
        deferredCommands = new List<HybridDbCommand>();

        this.migrator = migrator;
        this.DocumentStore = store;

        CommitId = tx?.CommitId ?? Guid.NewGuid();

        Enlist(tx);
    }

    internal DocumentSession(DocumentSession session) : this(session.DocumentStore, session.migrator, session.DocumentTransaction)
    {
        CommitId = session.CommitId;

        session.ManagedEntities.CopyTo(ManagedEntities);

        foreach (var data in session.SessionData)
        {
            SessionData.Add(data.Key, data.Value);
        }

        events.AddRange(session.events);
        deferredCommands.AddRange(session.deferredCommands);
    }

    public IDocumentStore DocumentStore { get; }

    public DocumentTransaction DocumentTransaction { get; private set; }

    public IReadOnlyList<HybridDbCommand> DeferredCommands => deferredCommands;

    public ManagedEntities ManagedEntities { get; }

    public IReadOnlyList<(int Generation, EventData<byte[]> Data)> Events => events;

    public Dictionary<object, object> SessionData { get; } = new();

    public void Defer(HybridDbCommand command) => deferredCommands.Add(command);

    public void Evict(object entity)
    {
        var managedEntity = TryGetManagedEntity(entity);

        if (managedEntity == null) return;

        ManagedEntities.Remove(managedEntity.EntityKey);
    }

    public Guid? GetEtagFor(object entity) => TryGetManagedEntity(entity)?.Etag;

    public bool Exists<T>(string key, out Guid? etag) where T : class => Exists(typeof(T), key, out etag);

    public bool Exists(Type type, string key, out Guid? etag)
    {
        if (TryGetManagedEntity(type, key, out var entity))
        {
            etag = entity.Etag;

            return true;
        }

        var design = DocumentStore.Configuration.GetOrCreateDesignFor(type);

        etag = Transactionally(tx => tx.Execute(new ExistsCommand(design.Table, key)));

        return etag != null;
    }

    public Dictionary<string, List<string>> GetMetadataFor(object entity) => TryGetManagedEntity(entity)?.Metadata;

    public void SetMetadataFor(object entity, Dictionary<string, List<string>> metadata)
    {
        var managedEntity = TryGetManagedEntity(entity);

        if (managedEntity == null) return;

        managedEntity.Metadata = metadata;
    }

    public IDocumentSession Copy() => new DocumentSession(this);

    public void Clear()
    {
        ManagedEntities.Clear();
        deferredCommands.Clear();
        SessionData.Clear();
        DocumentTransaction = null;
    }

    public bool TryGetManagedEntity<T>(string key, out T entity)
    {
        if (TryGetManagedEntity(typeof(T), key, out var entityObject))
        {
            entity = (T)entityObject.Entity;

            return true;
        }

        entity = default;

        return false;
    }

    public void Enlist(DocumentTransaction tx)
    {
        if (tx == null)
        {
            DocumentTransaction = null;

            return;
        }

        if (tx.CommitId != CommitId)
        {
            throw new ArgumentException("Cannot enlist in a transaction with another CommitId than the session.");
        }

        if (!ReferenceEquals(tx.Store, DocumentStore))
        {
            throw new ArgumentException("Cannot enlist in a transaction that does not originate from the same store as the session.");
        }

        DocumentTransaction = tx;
    }

    public Guid CommitId { get; private set; }

    public void SetDirty(object entity)
    {
        if (Advanced.ManagedEntities.TryGetValue(entity, out var managedEntity))
        {
            managedEntity.ForceWriteUnchangedDocument = true;
        }
    }

    public IAdvancedDocumentSession Advanced => this;

    public T Load<T>(string key, bool readOnly = false) where T : class => (T)Load(typeof(T), key, readOnly);

    public object Load(Type requestedType, string key, bool readOnly = false)
    {
        var results = Load(requestedType, new List<string> { key }, readOnly);

        return results.Count != 0 ? results.First() : null;
    }

    public IReadOnlyList<T> Load<T>(IReadOnlyList<string> keys, bool readOnly = false) where T : class =>
        Load(typeof(T), keys, readOnly).Cast<T>().ToList();

    public IReadOnlyList<object> Load(Type requestedType, IReadOnlyList<string> keys, bool readOnly = false)
    {
        var design = DocumentStore.Configuration.GetOrCreateDesignFor(requestedType);

        var result = new List<object>();
        var missingKeys = new List<string>();

        foreach (var key in keys)
        {
            if (!ManagedEntities.TryGetValue(new EntityKey(design.Table, key), out var managedEntity))
            {
                missingKeys.Add(key);

                continue;
            }

            if (readOnly && !managedEntity.ReadOnly)
            {
                throw new InvalidOperationException(
                    "Document can not be loaded as readonly, as it is already loaded or stored in session as writable.");
            }

            if (managedEntity.State == EntityState.Deleted) continue;

            var entityType = managedEntity.Entity.GetType();

            if (!requestedType.IsAssignableFrom(entityType))
            {
                throw new InvalidOperationException(
                    $"Document with id '{key}' exists, but is of type '{entityType}', which is not assignable to '{requestedType}'.");
            }

            result.Add(managedEntity.Entity);
        }

        var rows = Transactionally(tx => tx.Get(design.Table, missingKeys));

        foreach (var row in rows.Values)
        {
            var concreteDesign = DocumentStore.Configuration.GetOrCreateDesignByDiscriminator(design, (string)row[DocumentTable.DiscriminatorColumn]);

            result.Add(ConvertToEntityAndPutUnderManagement(requestedType, concreteDesign, row, readOnly));
        }

        return result;
    }

    /// <summary>
    /// Query for document of type T and subtypes of T in the table assigned to T.
    /// Note that if a subtype of T is specifically assigned to another table in store configuration,
    /// it will not be included in the result of Query&lt;T&gt;().
    /// </summary>
    public IQueryable<T> Query<T>() where T : class
    {
        var configuration = DocumentStore.Configuration;

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

    public IEnumerable<T> Query<T>(SqlBuilder sql) => Transactionally(x => x.Query<T>(sql));

    /// <summary>
    /// Store the entity in this <see cref="DocumentSession" /> as <see cref="EntityState.Transient" />.
    /// It will not be saved to the database until <see cref="SaveChanges()" /> is called.
    /// The entity is stored with an id retrived with the configured <see cref="Configuration.DefaultKeyResolver" />.
    /// When <see cref="SaveChanges()" /> is called, the entity will be INSERTED as a new document in the database table that
    /// is configured for the entity type.
    /// It is expected, at this time, that a document with the given id does not yet exist in the table, or else an
    /// <see cref="SqlException" /> will be thrown.
    /// </summary>
    /// <typeparam name="T">Type of the entity, used only for the return type.</typeparam>
    /// <param name="entity">The entity to store.</param>
    /// <returns>entity</returns>
    public T Store<T>(T entity) where T : class => Store(null, entity, null, EntityState.Transient);

    public T Store<T>(string key, T entity) where T : class => Store(key, entity, null, EntityState.Transient);

    /// <summary>
    /// Store the entity in this <see cref="DocumentSession" /> as <see cref="EntityState.Loaded" /> with the given etag.
    /// It will not be saved to the database until <see cref="SaveChanges()" /> is called.
    /// The entity is stored with an id retrived with the configured DefaultKeyResolver.
    /// When <see cref="SaveChanges()" /> is called, an existing document in the database table, that is configured for the
    /// entity type, will UPDATED with the changes from the entity.
    /// It is expected, at this time, that a document with the given id exists in the table, or else an
    /// <see cref="SqlException" /> will be thrown.
    /// If etag is a Guid it must match the etag of the existing document, or else a <see cref="ConcurrencyException" /> will
    /// be thrown.
    /// If etag is null, the existing document will be overridden.
    /// </summary>
    /// <param name="entity">The entity to store.</param>
    /// <param name="etag">Current etag or null.</param>
    /// <typeparam name="T">Type of the entity, used only for the return type.</typeparam>
    /// <returns>entity</returns>
    public T Store<T>(T entity, Guid? etag) where T : class => Store(null, entity, etag, EntityState.Loaded);

    public T Store<T>(string key, T entity, Guid? etag) where T : class => Store(key, entity, etag, EntityState.Loaded);

    public void Append(int generation, EventData<byte[]> @event) => events.Add((generation, @event));

    public void Delete(object entity)
    {
        var managedEntity = TryGetManagedEntity(entity);

        if (managedEntity == null) return;

        if (managedEntity.State == EntityState.Transient)
        {
            ManagedEntities.Remove(managedEntity.EntityKey);
        }
        else
        {
            managedEntity.State = EntityState.Deleted;
        }
    }

    public Guid SaveChanges() => SaveChanges(lastWriteWins: false, forceWriteUnchangedDocument: false);

    public Guid SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument)
    {
        if (saving)
        {
            throw new InvalidOperationException("Session is not in a valid state. Please dispose it and open a new one.");
        }

        saving = true;

        DocumentStore.Configuration.Notify(new SaveChanges_BeforePrepareCommands(this));

        var commands = new Dictionary<ManagedEntity, HybridDbCommand>();

        foreach (var managedEntity in ManagedEntities.Values.ToList())
        {
            if (managedEntity.ReadOnly) continue;

            var key = managedEntity.Key;
            var design = managedEntity.Design;

            var expectedEtag = !lastWriteWins ? managedEntity.Etag : null;

            switch (managedEntity.State)
            {
                case EntityState.Transient:
                {
                    var projections = CreateProjections(managedEntity);

                    var configuredVersion = projections.Get(DocumentTable.VersionColumn);
                    var document = (string)projections[DocumentTable.DocumentColumn];

                    commands.Add(managedEntity, new InsertCommand(design.Table, key, projections));
                    managedEntity.State = EntityState.Loaded;
                    managedEntity.Version = configuredVersion;
                    managedEntity.Document = document;

                    break;
                }
                case EntityState.Loaded:
                {
                    var projections = CreateProjections(managedEntity);

                    var configuredVersion = (int)projections[DocumentTable.VersionColumn];
                    var document = (string)projections[DocumentTable.DocumentColumn];
                    var metadataDocument = (string)projections[DocumentTable.MetadataColumn];

                    if (!forceWriteUnchangedDocument && !managedEntity.ForceWriteUnchangedDocument &&
                        SafeSequenceEqual(managedEntity.Document, document) &&
                        SafeSequenceEqual(managedEntity.MetadataDocument, metadataDocument))
                    {
                        break;
                    }

                    commands.Add(managedEntity, new UpdateCommand(design.Table, key, expectedEtag, projections));

                    if (configuredVersion != managedEntity.Version && !string.IsNullOrEmpty(managedEntity.Document))
                    {
                        DocumentStore.Configuration.BackupWriter.Write(
                            $"{design.DocumentType.FullName}_{key}_{managedEntity.Version}.bak",
                            Encoding.UTF8.GetBytes(managedEntity.Document));
                    }

                    managedEntity.Version = configuredVersion;
                    managedEntity.Document = document;

                    break;
                }
                case EntityState.Deleted:
                {
                    commands.Add(managedEntity, new DeleteCommand(design.Table, key, expectedEtag));
                    ManagedEntities.Remove(new EntityKey(design.Table, managedEntity.Key));

                    break;
                }
            }
        }

        if (DocumentStore.Configuration.EventStore)
        {
            var eventTable = DocumentStore.Configuration.Tables.Values.OfType<EventTable>().Single();

            foreach (var @event in events)
            {
                deferredCommands.Add(new AppendEvent(eventTable, @event.Generation, @event.Data));
            }
        }

        DocumentStore.Configuration.Notify(new SaveChanges_BeforeExecuteCommands(this, commands, deferredCommands));

        var executedCommands = Transactionally(resultingTx => deferredCommands
            .Concat(commands.Select(x => x.Value))
            .ToDictionary(command => command, command => DocumentStore.Execute(resultingTx, command)));

        DocumentStore.Configuration.Notify(new SaveChanges_AfterExecuteCommands(this, CommitId, executedCommands));

        deferredCommands.Clear();

        foreach (var managedEntity in commands.Keys)
        {
            managedEntity.Etag = CommitId;
        }

        saving = false;

        var commitId = CommitId;

        // We must update the CommitId upon saving. If we keep using the same session
        // and change the already loaded entities, they must get a new CommmitId/Etag when we save again.
        // If we did not do this, these last changes to entities could be overwriten by other actors.
        // But if we are in a transaction we keep the same CommitId as we assume that rows are locked
        // and no other actor can change them. We do not currently handle the case where we reuse the
        // same session across multiple transactions.
        CommitId = DocumentTransaction?.CommitId ?? Guid.NewGuid();

        return commitId;
    }

    public void Dispose() { }

    T Store<T>(string key, T entity, Guid? etag, EntityState state) where T : class
    {
        if (entity == null) return null;

        var design = DocumentStore.Configuration.GetOrCreateDesignFor(entity.GetType());

        key ??= design.GetKey(entity);

        var entityKey = new EntityKey(design.Table, key);

        if (ManagedEntities.TryGetValue(entityKey, out var managedEntity) ||
            ManagedEntities.TryGetValue(entity, out managedEntity))
        {
            // Storing a new instance under an existing id, is an error
            if (managedEntity.Entity != entity)
            {
                throw new HybridDbException($"Attempted to store a different object with id '{key}'.");
            }

            // Storing a same instance under an new id, is an error
            // Table cannot change as it's tied to entity's type
            if (!Equals(managedEntity.EntityKey, entityKey))
            {
                throw new HybridDbException($"Attempted to store same object '{managedEntity.Key}' with a different id '{key}'. Did you forget to evict?");
            }

            // Storing same instance is a noop
            return entity;
        }

        ManagedEntities.Add(new ManagedEntity(entityKey)
        {
            Design = design,
            Entity = entity,
            State = state,
            Etag = etag
        });

        return entity;
    }

    IDictionary<string, object> CreateProjections(ManagedEntity managedEntity) =>
        managedEntity.Design.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(managedEntity.Entity, managedEntity.Metadata));

    internal object ConvertToEntityAndPutUnderManagement(Type requestedType, DocumentDesign concreteDesign, IDictionary<string, object> row, bool readOnly)
    {
        var key = (string)row[DocumentTable.IdColumn];
        var entityKey = new EntityKey(concreteDesign.Table, key);

        if (ManagedEntities.TryGetValue(entityKey, out var existingManagedEntity))
        {
            if (existingManagedEntity.State == EntityState.Deleted) return null;

            return existingManagedEntity.Entity;
        }

        var document = (string)row[DocumentTable.DocumentColumn];
        var documentVersion = (int)row[DocumentTable.VersionColumn];

        var entity = migrator.DeserializeAndMigrate(this, concreteDesign, row);
        var metadataDocument = (string)row[DocumentTable.MetadataColumn];
        var metadata = metadataDocument != null
            ? (Dictionary<string, List<string>>)DocumentStore.Configuration.Serializer.Deserialize(metadataDocument, typeof(Dictionary<string, List<string>>))
            : null;

        if (entity is DocumentMigrator.DeletedDocument)
        {
            var condemnedManagedEntity = new ManagedEntity(entityKey)
            {
                Design = concreteDesign,
                Entity = entity,
                Document = document,
                Metadata = metadata,
                MetadataDocument = metadataDocument,
                Etag = (Guid)row[DocumentTable.EtagColumn],
                Version = documentVersion,
                State = EntityState.Deleted
            };

            ManagedEntities.Add(condemnedManagedEntity);

            AssertRequestedTypeMatches(requestedType, condemnedManagedEntity);

            return null;
        }

        var managedEntity = new ManagedEntity(entityKey)
        {
            Design = concreteDesign,
            Entity = entity,
            Document = document,
            Metadata = metadata,
            MetadataDocument = metadataDocument,
            Etag = (Guid)row[DocumentTable.EtagColumn],
            Version = documentVersion,
            State = EntityState.Loaded,
            ReadOnly = readOnly
        };

        DocumentStore.Configuration.Notify(new EntityLoaded(this, requestedType, managedEntity));

        AssertRequestedTypeMatches(requestedType, managedEntity);
        AssertDeserializedDocumentMatches(managedEntity);

        ManagedEntities.Add(managedEntity);

        return managedEntity.Entity;
    }

    static void AssertDeserializedDocumentMatches(ManagedEntity managedEntity)
    {
        // The deserialized entity must match the the design dictated by the rows discriminator
        if (managedEntity.Entity.GetType() != managedEntity.Design.DocumentType)
        {
            throw new InvalidOperationException(
                $"Requested a document of type '{managedEntity.Design.DocumentType}', but got a '{managedEntity.Entity.GetType()}'.");
        }
    }

    static void AssertRequestedTypeMatches(Type requestedType, ManagedEntity managedEntity)
    {
        // The design dictated by the rows discriminator must assignable to the requested type
        if (!requestedType.IsAssignableFrom(managedEntity.Design.DocumentType))
        {
            throw new InvalidOperationException(
                $"Document with id '{managedEntity.Key}' exists, but is of type '{managedEntity.Design.DocumentType}', which is not assignable to '{requestedType}'.");
        }
    }

    internal T Transactionally<T>(Func<DocumentTransaction, T> func) =>
        DocumentTransaction != null
            ? func(DocumentTransaction)
            : DocumentStore.Transactionally(CommitId, func);

    public bool TryGetManagedEntity(Type type, string key, out ManagedEntity entity) =>
        ManagedEntities.TryGetValue(new EntityKey(DocumentStore.Configuration.GetOrCreateDesignFor(type).Table, key), out entity);

    ManagedEntity TryGetManagedEntity(object entity) =>
        ManagedEntities.TryGetValue(entity, out var managedEntity)
            ? managedEntity
            : null;

    bool SafeSequenceEqual<T>(IEnumerable<T> first, IEnumerable<T> second)
    {
        if (Equals(first, second))
        {
            return true;
        }

        if (first == null || second == null)
        {
            return false;
        }

        return first.SequenceEqual(second);
    }
}