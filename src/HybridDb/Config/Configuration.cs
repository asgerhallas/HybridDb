using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Commands;
using HybridDb.Events;
using HybridDb.Events.Commands;
using HybridDb.Migrations;
using HybridDb.Migrations.Documents;
using HybridDb.Migrations.Schema;
using HybridDb.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShinySwitch;
using static Indentional.Indent;

namespace HybridDb.Config
{
    public class Configuration : ConfigurationContainer
    {
        const string MetadataTableName = "HybridDb";

        readonly object gate = new();

        readonly ConcurrentDictionary<string, Table> tables;
        readonly List<DocumentDesign> documentDesigns;

        public Configuration()
        {
            ConnectionString = "data source=.;Integrated Security=True";

            tables = new ConcurrentDictionary<string, Table>();
            documentDesigns = new List<DocumentDesign>();

            Logger = NullLoggerProvider.Instance.CreateLogger("HybridDb");

            UseSerializer(new DefaultSerializer());
            UseTypeMapper(new ShortNameTypeMapper());

            Migrations = new List<Migration>();
            BackupWriter = new NullBackupWriter();
            RunUpfrontMigrations = true;
            RunBackgroundMigrations = true;
            TableNamePrefix = "";
            DefaultKeyResolver = KeyResolver;
            EventStore = false;
            ColumnNameConvention = ColumnNameBuilder.GetColumnNameByConventionFor;

            Register<Func<DocumentStore, DdlCommand, Action>>(container => (store, command) => () => command.Execute(store));

            Register<Func<DocumentTransaction, DmlCommand, Func<object>>>(container => (tx, command) => () => 
                Switch<object>.On(command)
                    .Match<InsertCommand>(insertCommand => InsertCommand.Execute(tx, insertCommand))
                    .Match<UpdateCommand>(updateCommand => UpdateCommand.Execute(tx, updateCommand))
                    .Match<DeleteCommand>(deleteCommand => DeleteCommand.Execute(tx, deleteCommand))
                    .Match<GetCommand>(getCommand => GetCommand.Execute(tx, getCommand))
                    .Match<ExistsCommand>(existsCommand => ExistsCommand.Execute(tx, existsCommand))
                    .OrThrow(new ArgumentOutOfRangeException($"No executor registered for {command.GetType()}.")));

            Register(_ => new DocumentMigrator(this));
        }

        public ITypeMapper TypeMapper => Resolve<ITypeMapper>();

        internal bool IsInitialized { get; private set; }

        public string ConnectionString { get; private set; }
        public ILogger Logger { get; private set; }
        public ISerializer Serializer { get; private set; }
        public IReadOnlyList<Migration> Migrations { get; private set; }
        public IBackupWriter BackupWriter { get; private set; }
        public bool RunUpfrontMigrations { get; private set; }
        public bool RunBackgroundMigrations { get; private set; }
        public int ConfiguredVersion { get; private set; }
        public string TableNamePrefix { get; private set; }
        public Func<object, string> DefaultKeyResolver { get; private set; }
        public bool SoftDelete { get; private set; }
        public bool EventStore { get; private set; }
        public Func<Expression, string> ColumnNameConvention { get; private set; }

        public IReadOnlyDictionary<string, Table> Tables => tables.ToDictionary();
        public IReadOnlyList<DocumentDesign> DocumentDesigns => documentDesigns;

        static string GetTableNameByConventionFor(Type type) => Inflector.Inflector.Pluralize(type.Name);

        internal void Initialize()
        {
            if (IsInitialized) return;

            lock (gate)
            {
                // add this first in the collection, but after all other designs has been registered, so no registered document falls back to Document table
                documentDesigns.Insert(0, new DocumentDesign(this, GetOrAddDocumentTable("Documents"), typeof(object), "object"));

                // add a table for metadata
                GetOrAddTable(new Table(MetadataTableName, new Column("SchemaVersion", typeof(int))));

                IsInitialized = true;
            }
        }

        public DocumentDesigner<TEntity> Document<TEntity>(string tablename = null, string discriminator = null) => 
            new DocumentDesigner<TEntity>(GetOrCreateDesignFor(typeof(TEntity), tablename, discriminator), ColumnNameConvention);

        public DocumentDesign GetOrCreateDesignFor(Type type, string tablename = null, string discriminator = null)
        {
            lock (gate)
            {
                discriminator ??= TypeMapper.ToDiscriminator(type);

                // for interfaces we find the first design for a class that is assignable to the interface or fallback to the design for typeof(object)
                if (type.IsInterface)
                {
                    return DocumentDesigns.FirstOrDefault(x => type.IsAssignableFrom(x.DocumentType)) ?? DocumentDesigns[0];
                }

                //TODO: Table equals base design... model it?
                var existing = TryGetDesignFor(type);

                // no design for type, nor a base design, add new table and base design
                if (existing == null)
                {
                    return AddDesign(new DocumentDesign(
                        this, GetOrAddDocumentTable(tablename ?? GetTableNameByConventionFor(type)),
                        type, discriminator));
                }

                // design already exists for type
                if (existing.DocumentType == type)
                {
                    if (tablename == null || tablename == existing.Table.Name)
                        return existing;

                    throw new InvalidOperationException(_($@"
                        Design already exists for type '{type}' but is not assigned to the specified tablename '{tablename}'.
                        The existing design for '{type}' is assigned to table '{existing.Table.Name}'."));
                }

                // we now know that type is a subtype to existing
                // there is explicitly given a table name, so we add a new table for the derived type
                if (tablename != null && tablename != existing.Table.Name)
                {
                    return AddDesign(new DocumentDesign(
                        this, GetOrAddDocumentTable(tablename),
                        type, discriminator));
                }

                // a table and base design exists for type, add the derived type as a child design
                return AddDesign(new DocumentDesign(this, existing, type, discriminator));
            }
        }

        public DocumentDesign GetOrCreateDesignByDiscriminator(DocumentDesign design, string discriminator)
        {
            lock (gate)
            {
                if (design.DecendentsAndSelf.TryGetValue(discriminator, out var concreteDesign))
                    return concreteDesign;

                var type = TypeMapper.ToType(design.Root.DocumentType, discriminator);

                if (type == null)
                {
                    throw new InvalidOperationException($"No concrete type could be mapped from discriminator '{discriminator}'.");
                }

                return GetOrCreateDesignFor(type, discriminator: discriminator);
            }
        }

        public DocumentDesign GetDesignFor<T>()
        {
            var design = TryGetDesignFor(typeof(T));
            if (design != null) return design;

            throw new HybridDbException(string.Format(
                "No design was registered for documents of type {0}. " +
                "Please run store.Document<{0}>() to register it before use.",
                typeof(T).Name));
        }

        public DocumentDesign TryGetDesignFor(Type type)
        {
            // get most specific type by searching backwards
            lock (gate)
            {
                return DocumentDesigns.LastOrDefault(x => x.DocumentType.IsAssignableFrom(type));
            }
        }

        public DocumentDesign GetExactDesignFor(Type type)
        {
            lock (gate)
            {
                return DocumentDesigns.First(x => x.DocumentType == type);
            }
        }

        public DocumentDesign TryGetDesignByTablename(string tablename)
        {
            lock (gate)
            {
                return DocumentDesigns.FirstOrDefault(x => x.Table.Name == tablename);
            }
        }

        public Table GetOrAddTable(Table table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            return tables.GetOrAdd(table.Name, name =>
            {
                if (IsInitialized)
                {
                    throw new InvalidOperationException($"You can not register the table '{name}' after store has been initialized.");
                }

                return table;
            });
        }

        public Table GetMetadataTable() => Tables[MetadataTableName];

        public void UseConnectionString(string connectionString) => ConnectionString = connectionString;

        public void UseLogger(ILogger logger) => Logger = logger;

        public void UseSerializer(ISerializer serializer) => Serializer = serializer;

        public void UseTypeMapper(ITypeMapper typeMapper)
        {
            lock (gate)
            {
                if (DocumentDesigns.Any())
                    throw new InvalidOperationException("Please call UseTypeMapper() before any documents are configured.");

                Register<ITypeMapper>(_ => new CachedTypeMapper(typeMapper));
            }
        }

        public void UseMigrations(IReadOnlyList<Migration> migrations)
        {
            Migrations = migrations.OrderBy(x => x.Version).Where((x, i) =>
            {
                var expectedVersion = i + 1;

                if (x.Version == expectedVersion)
                    return true;

                throw new ArgumentException($"Missing migration for version {expectedVersion}.");
            }).ToList();

            ConfiguredVersion = Migrations.Any() ? Migrations.Last().Version : 0;
        }

        public void UseBackupWriter(IBackupWriter backupWriter) => BackupWriter = backupWriter;

        public void UseTableNamePrefix(string prefix) => TableNamePrefix = prefix;

        public void UseKeyResolver(Func<object, string> resolver) => DefaultKeyResolver = resolver;

        public void UseSoftDelete() => SoftDelete = true;

        public void UseEventStore(string tableName = "events")
        {
            EventStore = true;

            tables.TryAdd(tableName, new EventTable(tableName));

            Decorate<Func<DocumentTransaction, DmlCommand, Func<object>>>((container, decoratee) => (tx, command) => () => 
                Switch<object>.On(command)
                    .Match<AppendEvent>(appendEvent => AppendEvent.Execute(tx, appendEvent))
                    .Match<ReadStream>(readStream => ReadStream.Execute(tx, readStream))
                    .Match<ReadEvents>(readEvents => ReadEvents.Execute(tx, readEvents))
                    .Match<ReadEventsByCommitIds>(readEvents => ReadEventsByCommitIds.Execute(tx, readEvents))
                    .Match<GetPositionOf>(getPosition => GetPositionOf.Execute(tx, getPosition))
                    .Match<LoadParentCommit>(loadParentCommit => LoadParentCommit.Execute(tx, loadParentCommit))
                    .Else(() => decoratee(tx, command)()));
        }

        public void UseColumnNameConventions(Func<Expression, string> convention) => ColumnNameConvention = convention;

        internal void DisableMigrations()
        {
            RunUpfrontMigrations = false;
            RunBackgroundMigrations = false;
        }

        /// <summary>
        /// This will disable the background process that loads and migrates rows/documents,
        /// but a document will still be migrated when it is loaded into a session.
        /// </summary>
        public void DisableBackgroundMigrations() => RunBackgroundMigrations = false;

        static string KeyResolver(object entity)
        {
            var id = ((dynamic) entity).Id;
            return id != null ? id.ToString() : Guid.NewGuid().ToString();
        }

        DocumentDesign AddDesign(DocumentDesign design)
        {
            if (DocumentDesigns.Any(x => Equals(x.Table, design.Table) && x.Discriminator == design.Discriminator))
            {
                throw new InvalidOperationException(_(@$"
                    Document '{design.DocumentType.Name}' has discriminator '{design.Discriminator}' in table '{design.Table}'.
                    This combination already exists, please select either another table or discriminator for the type."));
            }

            if (design.Parent != null)
            {
                documentDesigns.Insert(documentDesigns.IndexOf(design.Parent) + 1, design);
            }
            else
            {
                if (DocumentDesigns.FirstOrDefault(x => design.DocumentType.IsAssignableFrom(x.DocumentType)) is { } existingDesign)
                    throw new InvalidOperationException($"Document '{design.DocumentType.Name}' must be configured before its subtype '{existingDesign.DocumentType}'.");

                documentDesigns.Add(design);
            }

            return design;

        }

        DocumentTable GetOrAddDocumentTable(string tablename)
        {
            if (tablename == null) throw new ArgumentNullException(nameof(tablename));

            return (DocumentTable) GetOrAddTable(new DocumentTable(tablename));
        }

        public Action GetDdlCommandExecutor(DocumentStore store, DdlCommand command) => 
            Resolve<Func<DocumentStore, DdlCommand, Action>>()(store, command);

        public Func<object> GetDmlCommandExecutor(DocumentTransaction tx, DmlCommand command) => 
            Resolve<Func<DocumentTransaction, DmlCommand, Func<object>>>()(tx, command);
    }
}