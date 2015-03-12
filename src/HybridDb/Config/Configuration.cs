using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using HybridDb.Logging;
using HybridDb.Migrations;

namespace HybridDb.Config
{
    public class Configuration
    {
        readonly ConcurrentDictionary<Type, DocumentDesign> documentDesignsCache;

        internal Configuration()
        {
            Tables = new ConcurrentDictionary<string, Table>();
            DocumentDesigns = new List<DocumentDesign>();
            documentDesignsCache = new ConcurrentDictionary<Type, DocumentDesign>();

            Logger = new ConsoleLogger(LogLevel.Info, new LoggingColors());
            Serializer = new DefaultBsonSerializer();
            MigrationProvider = new StaticMigrationProvider();
        }

        public ILogger Logger { get; private set; }
        public ISerializer Serializer { get; private set; }
        public IMigrationProvider MigrationProvider { get; private set; }

        internal ConcurrentDictionary<string, Table> Tables { get; private set; }
        internal List<DocumentDesign> DocumentDesigns { get; private set; }

        static string GetTableNameByConventionFor(Type type)
        {
            return Inflector.Inflector.Pluralize(type.Name);
        }

        public DocumentDesigner<TEntity> Document<TEntity>(string tablename = null, string discriminator = null)
        {
            var design = CreateDesignFor(typeof (TEntity), tablename, discriminator);
            return new DocumentDesigner<TEntity>(design);
        }

        public DocumentDesign CreateDesignFor(Type type, string tablename = null, string discriminator = null)
        {
            tablename = tablename ?? GetTableNameByConventionFor(type);
            discriminator = discriminator ?? type.Name;

            var child = TryGetDesignFor(type);
            if (child != null)
            {
                throw new InvalidOperationException(string.Format(
                    "Document {0} must be configured before its subtype {1}.", type, child.DocumentType));
            }

            var parent = DocumentDesigns.LastOrDefault(x => x.DocumentType.IsAssignableFrom(type));

            var design = parent != null
                ? new DocumentDesign(this, parent, type, discriminator)
                : new DocumentDesign(this, AddTable(tablename), type, discriminator);

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
                "No table was registered for type {0}. " +
                "Please run store.Document<{0}>() to register it before use.", 
                type.Name));
        }

        public DocumentDesign TryGetDesignFor<T>()
        {
            return TryGetDesignFor(typeof(T));
        }

        public DocumentDesign TryGetDesignFor(Type type)
        {
            DocumentDesign design;
            if (documentDesignsCache.TryGetValue(type, out design))
                return design;

            var match = DocumentDesigns.FirstOrDefault(x => type.IsAssignableFrom(x.DocumentType));
            
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

        public void UseMigrations(IMigrationProvider provider)
        {
            MigrationProvider = provider;
        }

        DocumentTable AddTable(string tablename)
        {
            return (DocumentTable)Tables.GetOrAdd(tablename, name => new DocumentTable(name));
        }
    }
}