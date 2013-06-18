using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using HybridDb.Logging;
using HybridDb.Schema;

namespace HybridDb
{
    public class Configuration
    {
        public Configuration()
        {
            Tables = new List<Table>();
            DocumentDesigns = new ConcurrentDictionary<Type, DocumentDesign>();
            Serializer = new DefaultBsonSerializer();
            Logger = new ConsoleLogger(LogLevel.Info, new LoggingColors());

            var metadata = new Table("HybridDb");
            metadata.Register(new Column("Table", new SqlColumn(DbType.AnsiStringFixedLength, 255)));
            metadata.Register(new Column("SchemaVersion", new SqlColumn(DbType.Int32)));
            metadata.Register(new Column("DocumentVersion", new SqlColumn(DbType.Int32)));

            Add(metadata);
        }

        public ILogger Logger { get; private set; }
        public ISerializer Serializer { get; private set; }

        public List<Table> Tables { get; private set; }
        public ConcurrentDictionary<Type, DocumentDesign> DocumentDesigns { get; private set; }

        public void Add(Table table)
        {
            Tables.Add(table);
        }

        public void Add(DocumentDesign design)
        {
            Add(design.Table);
            DocumentDesigns.TryAdd(design.Type, design);
        }

        public DocumentDesign<T> GetDesignFor<T>()
        {
            return (DocumentDesign<T>) GetDesignFor(typeof(T));
        }

        public DocumentDesign GetDesignFor(Type type)
        {
            DocumentDesign table;
            if (!DocumentDesigns.TryGetValue(type, out table))
                throw new TableNotFoundException(type);
                
            return table;
        }

        public string GetTableNameByConventionFor<TEntity>()
        {
            return Inflector.Inflector.Pluralize(typeof(TEntity).Name);
        }

        public string GetColumnNameByConventionFor(Expression projector)
        {
            var columnNameBuilder = new ColumnNameBuilder();
            columnNameBuilder.Visit(projector);
            return columnNameBuilder.ColumnName;
        }

        public void UseSerializer(ISerializer serializer)
        {
            Serializer = serializer;
        }

        public void UseLogger(ILogger logger)
        {
            Logger = logger;
        }
    }
}