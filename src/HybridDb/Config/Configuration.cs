using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Migrations;
using HybridDb.Serialization;
using Serilog;
using Serilog.Core;

namespace HybridDb.Config
{
    public class Configuration
    {
        bool initialized = false;

        internal Configuration()
        {
            Tables = new ConcurrentDictionary<string, Table>();
            DocumentDesigns = new List<DocumentDesign>();

            Logger = Log.Logger;
            //Logger = new LoggerConfiguration()
            //    .MinimumLevel.Debug()
            //    .WriteTo.ColoredConsole()
            //    .CreateLogger();

            Serializer = new DefaultSerializer();
            TypeMapper = new AssemblyQualifiedNameTypeMapper();
            Migrations = new List<Migration>();
            BackupWriter = new NullBackupWriter();
            RunSchemaMigrationsOnStartup = true;
            RunDocumentMigrationsOnStartup = true;
            TableNamePrefix = "";
            DefaultKeyResolver = KeyResolver;
        }

        public ILogger Logger { get; private set; }
        public ISerializer Serializer { get; private set; }
        public ITypeMapper TypeMapper { get; private set; }
        public IReadOnlyList<Migration> Migrations { get; private set; }
        public IBackupWriter BackupWriter { get; private set; }
        public bool RunSchemaMigrationsOnStartup { get; private set; }
        public bool RunDocumentMigrationsOnStartup { get; private set; }
        public int ConfiguredVersion { get; private set; }
        public string TableNamePrefix { get; private set; }
        public Func<object, string> DefaultKeyResolver { get; private set; }

        internal ConcurrentDictionary<string, Table> Tables { get; }
        internal List<DocumentDesign> DocumentDesigns { get; }

        static string GetTableNameByConventionFor(Type type)
        {
            return Inflector.Inflector.Pluralize(type.Name);
        }

        internal void Initialize()
        {
            DocumentDesigns.Insert(0, new DocumentDesign(this, AddTable("Documents"), typeof(object), "object"));
            initialized = true;
        }

        public DocumentDesigner<TEntity> Document<TEntity>(string tablename = null)
        {
            return new DocumentDesigner<TEntity>(CreateDesignFor(typeof (TEntity), tablename));
        }

        public DocumentDesign CreateDesignFor(Type type, string tablename = null)
        {
            var discriminator = TypeMapper.ToDiscriminator(type);

            var parent = TryGetDesignFor(type);

            if (parent != null && tablename == null)
            {
                var design = new DocumentDesign(this, parent, type, discriminator);

                var afterParent = DocumentDesigns.IndexOf(parent) + 1;
                DocumentDesigns.Insert(afterParent, design);

                return design;
            }
            else
            {
                tablename = tablename ?? GetTableNameByConventionFor(type);

                if (initialized)
                {
                    throw new InvalidOperationException($"You can not register the table '{tablename}' after store has been initialized.");
                }

                var existingDesign = DocumentDesigns.FirstOrDefault(existing => type.IsAssignableFrom(existing.DocumentType));
                if (existingDesign != null)
                {
                    throw new InvalidOperationException($"Document {type.Name} must be configured before its subtype {existingDesign.DocumentType}.");
                }

                var design = new DocumentDesign(this, AddTable(tablename), type, discriminator);
                DocumentDesigns.Add(design);
                return design;
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
            return DocumentDesigns.LastOrDefault(x => x.DocumentType.IsAssignableFrom(type));
        }

        public DocumentDesign TryGetExactDesignFor(Type type)
        {
            return DocumentDesigns.FirstOrDefault(x => x.DocumentType == type);
        }

        public DocumentDesign TryGetLeastSpecificDesignFor(Type type)
        {
            // get _least_ specific design that can be assigned to given type.
            // e.g. Load<BaseType>() gets design for BaseType if registered, not DerivedType.
            // from the BaseType we can use the discriminator from the loaded document to find the concrete design.
            return DocumentDesigns.FirstOrDefault(x => type.IsAssignableFrom(x.DocumentType)) ?? DocumentDesigns[0];
        }

        public DocumentDesign GetOrCreateConcreteDesign(DocumentDesign @base, string discriminator, string key)
        {
            DocumentDesign concreteDesign;
            if (@base.DecendentsAndSelf.TryGetValue(discriminator, out concreteDesign))
                return concreteDesign;

            var type = TypeMapper.ToType(discriminator);

            if (type == null)
            {
                throw new InvalidOperationException($"Document with id '{key}' exists, but no concrete type was found for discriminator '{discriminator}'.");
            }

            if (!@base.DocumentType.IsAssignableFrom(type))
            {
                return null;
                
            }

            return CreateDesignFor(type);
        }

        public void UseLogger(ILogger logger)
        {
            Logger = logger;
        }

        public void UseSerializer(ISerializer serializer)
        {
            Serializer = serializer;
        }

        public void UseTypeMapper(ITypeMapper typeMapper)
        {
            if (DocumentDesigns.Any())
                throw new InvalidOperationException("Please call UseTypeMapper() before any documents are configured.");

            TypeMapper = typeMapper;
        }

        public void UseMigrations(IReadOnlyList<Migration> migrations)
        {
            Migrations = migrations.OrderBy(x => x.Version).Where((x, i) =>
            {
                var expectedVersion = i+1;
                
                if (x.Version == expectedVersion)
                    return true;
                
                throw new ArgumentException($"Missing migration for version {expectedVersion}.");
            }).ToList();

            ConfiguredVersion = Migrations.Any() ? Migrations.Last().Version : 0;
        }

        public void UseBackupWriter(IBackupWriter backupWriter)
        {
            BackupWriter = backupWriter;
        }

        public void UseTableNamePrefix(string prefix)
        {
            TableNamePrefix = prefix;
        }

        public void UseKeyResolver(Func<object, string> resolver)
        {
            DefaultKeyResolver = resolver;
        }

        internal void DisableMigrationsOnStartup()
        {
            RunSchemaMigrationsOnStartup = false;
            RunDocumentMigrationsOnStartup = false;
        }

        public void DisableDocumentMigrationsOnStartup()
        {
            RunDocumentMigrationsOnStartup = false;
        }

        static string KeyResolver(object entity)
        {
            var id = ((dynamic)entity).Id;
            return id != null ? id.ToString() : Guid.NewGuid().ToString();
        }

        DocumentTable AddTable(string tablename)
        {
            if (tablename == null)
                throw new ArgumentException("Tablename must be provided.");

            return (DocumentTable)Tables.GetOrAdd(tablename, name => new DocumentTable(name));
        }
    }
}