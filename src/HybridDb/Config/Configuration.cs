using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Migrations;
using HybridDb.Serialization;
using Serilog;

namespace HybridDb.Config
{
    public class Configuration
    {
        readonly ConcurrentDictionary<Type, DocumentDesign> documentDesignsCache;

        bool initialized = false;

        internal Configuration()
        {
            Tables = new ConcurrentDictionary<string, Table>();
            DocumentDesigns = new List<DocumentDesign>();
            documentDesignsCache = new ConcurrentDictionary<Type, DocumentDesign>();

            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.ColoredConsole()
                .CreateLogger();

            Serializer = new DefaultSerializer();
            Migrations = new List<Migration>();
            BackupWriter = new NullBackupWriter();
            RunSchemaMigrationsOnStartup = true;
            RunDocumentMigrationsOnStartup = true;
        }

        public ILogger Logger { get; private set; }
        public ISerializer Serializer { get; private set; }
        public IReadOnlyList<Migration> Migrations { get; private set; }
        public IBackupWriter BackupWriter { get; private set; }
        public bool RunSchemaMigrationsOnStartup { get; private set; }
        public bool RunDocumentMigrationsOnStartup { get; private set; }
        public int ConfiguredVersion { get; private set; }
        public string TableNamePrefix { get; private set; }

        internal ConcurrentDictionary<string, Table> Tables { get; }
        internal List<DocumentDesign> DocumentDesigns { get; }

        static string GetTableNameByConventionFor(Type type)
        {
            return Inflector.Inflector.Pluralize(type.Name);
        }

        public void Initialize()
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
            var discriminator = type.AssemblyQualifiedName;

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

        public DocumentDesign GetOrCreateDesignFor(Type type)
        {
            var discriminator = type.AssemblyQualifiedName;

            var parent = TryGetDesignFor(type);

            if (parent == null)
            {
                throw new InvalidOperationException();
            }

            var design = new DocumentDesign(this, parent, type, discriminator);

            DocumentDesigns.Add(design);
            return design;
        }

        public DocumentDesign GetDesignFor<T>()
        {
            return GetDesignFor(typeof(T));
        }

        public DocumentDesign GetDesignFor(Type type)
        {
            var design = TryGetDesignFor(type);
            if (design != null) return design;

            throw new HybridDbException(string.Format(
                "No design was registered for documents of type {0}. " +
                "Please run store.Document<{0}>() to register it before use.", 
                type.Name));
        }

        public DocumentDesign TryGetDesignFor<T>()
        {
            return TryGetDesignFor(typeof(T));
        }

        public DocumentDesign TryGetDesignFor(Type type)
        {
            //DocumentDesign design;
            //if (documentDesignsCache.TryGetValue(type, out design))
            //    return design;

            // get most specific 
            var match = DocumentDesigns.LastOrDefault(x => x.DocumentType.IsAssignableFrom(type));
            
            // must never associate a type to null in the cache, the design might be added later
            if (match != null)
            {
                documentDesignsCache.TryAdd(type, match);
            }

            return match;
        }

        public DocumentDesign GetExactDesignFor(Type type)
        {
            var design = TryGetExactDesignFor(type);

            if (design != null) return design;

            throw new HybridDbException(string.Format(
                "No design was registered for documents of type {0}. " +
                "Please run store.Document<{0}>() to register it before use.",
                type.Name));
        }

        public DocumentDesign TryGetExactDesignFor(Type type)
        {
            return DocumentDesigns.FirstOrDefault(x => x.DocumentType == type);
        }

        public DocumentDesign TryGetLeastSpecificDesignFor(Type type)
        {
            //DocumentDesign design;
            //if (documentDesignsCache.TryGetValue(type, out design))
            //    return design;

            // get _least_ specific design that can be assigned to given type.
            // e.g. Load<BaseType>() gets design for BaseType if registered, not DerivedType.
            // from the BaseType we can use the discriminator from the loaded document to find the concrete design.
            var match = DocumentDesigns.FirstOrDefault(x => type.IsAssignableFrom(x.DocumentType)) ?? DocumentDesigns[0];

            // must never associate a type to null in the cache, the design might be added later
            if (match != null)
            {
                documentDesignsCache.TryAdd(type, match);
            }

            return match;
        }

        public void UseSerializer(ISerializer serializer)
        {
            Serializer = serializer;
        }

        public void UseLogger(ILogger logger)
        {
            Logger = logger;
        }

        public void UseMigrations(IReadOnlyList<Migration> migrations)
        {
            Migrations = migrations.OrderBy(x => x.Version).Where((x, i) =>
            {
                var expectedVersion = i+1;
                
                if (x.Version == expectedVersion)
                    return true;
                
                throw new ArgumentException(string.Format("Missing migration for version {0}.", expectedVersion));
            }).ToList();

            ConfiguredVersion = Migrations.Any() ? Migrations.Last().Version : 0;
        }

        public void UseBackupWriter(IBackupWriter backupWriter)
        {
            BackupWriter = backupWriter;
        }

        public void UseTableNamePrefix(string prefix)
        {
            if (prefix == "")
                throw new ArgumentException("Prefix must not be empty string.");

            TableNamePrefix = prefix;
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

        DocumentTable AddTable(string tablename)
        {
            if (tablename == null)
                throw new ArgumentException("Tablename must be provided.");

            return (DocumentTable)Tables.GetOrAdd(tablename, name => new DocumentTable(name));
        }
    }
}