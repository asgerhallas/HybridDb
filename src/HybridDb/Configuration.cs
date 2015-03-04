using System;
using System.Collections.Concurrent;
using System.Data;
using System.Linq;
using HybridDb.Logging;
using HybridDb.Schema;

namespace HybridDb
{
    class Configurator : HybridDbConfigurator
    {
        protected override void Configure()
        {
            Document<string>().With(x => x.Length);
        }
    }

    public class Configuration
    {
        internal Configuration()
        {
            Tables = new ConcurrentDictionary<string, Table>();
            DocumentDesigns = new ConcurrentDictionary<Type, DocumentDesign>();
            Indexes = new ConcurrentDictionary<Type, DocumentDesign>();

            Serializer = new DefaultBsonSerializer();
            Logger = new ConsoleLogger(LogLevel.Info, new LoggingColors());

            var metadata = new Table("HybridDb");
            metadata.Register(new Column("Table", new SqlColumn(DbType.AnsiStringFixedLength, 255)));
            metadata.Register(new Column("SchemaVersion", new SqlColumn(DbType.Int32)));
            metadata.Register(new Column("DocumentVersion", new SqlColumn(DbType.Int32)));

            Tables.TryAdd(metadata.Name, metadata);
        }

        internal static Configuration Create(IHybridDbConfigurator configurator)
        {
            var configuration = new Configuration();
            configurator = configurator ?? new LambdaHybridDbConfigurator(x => { });
            configurator.Configure(configuration);
            return configuration;
        }

        public ILogger Logger { get; private set; }
        public ISerializer Serializer { get; private set; }

        public ConcurrentDictionary<string, Table> Tables { get; private set; }
        public ConcurrentDictionary<Type, DocumentDesign> DocumentDesigns { get; private set; }
        public ConcurrentDictionary<Type, DocumentDesign> Indexes { get; private set; }

        static string GetTableNameByConventionFor(Type type)
        {
            return Inflector.Inflector.Pluralize(type.Name);
        }

        public DocumentDesigner<TEntity> Document<TEntity>(string tablename = null)
        {
            var design = CreateDesignFor(typeof (TEntity));
            return new DocumentDesigner<TEntity>(design);
        }

        public IndexDesigner<TIndex, TEntity> Index<TIndex, TEntity>()
        {
            var design = GetDesignFor<TEntity>();
            
            Indexes.TryAdd(typeof(TIndex), design);

            return new IndexDesigner<TIndex, TEntity>(design);
        }

        public DocumentDesign CreateDesignFor(Type type, string tablename = null)
        {
            tablename = tablename ?? GetTableNameByConventionFor(type);

            var baseDesign = DocumentDesigns
                .Where(x => x.Key.IsAssignableFrom(type))
                .Select(x => x.Value)
                .SingleOrDefault();

            var design = baseDesign != null
                ? new DocumentDesign(this, baseDesign, type)
                : new DocumentDesign(this, AddTable(tablename), type);

            DocumentDesigns.TryAdd(design.DocumentType, design);
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
            return DocumentDesigns.TryGetValue(type, out design) ? design : null;
        }

        public DocumentDesign TryGetDesignForIndex(Type type)
        {
            DocumentDesign design;
            return Indexes.TryGetValue(type, out design) ? design : null;
        }

        public DocumentDesign GetOrCreateDesignFor(Type type)
        {
            return TryGetDesignFor(type) ?? CreateDesignFor(type);
        }

        public void UseSerializer(ISerializer serializer)
        {
            Serializer = serializer;
        }

        public void UseLogger(ILogger logger)
        {
            Logger = logger;
        }

        DocumentTable AddTable(string tablename)
        {
            return (DocumentTable)Tables.GetOrAdd(tablename, name => new DocumentTable(name));
        }

    }
}