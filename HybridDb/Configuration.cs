using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Logging;
using HybridDb.Schema;

namespace HybridDb
{
    public class Configuration
    {
        readonly ConcurrentDictionary<Type, DocumentConfiguration> tables;

        public Configuration()
        {
            tables = new ConcurrentDictionary<Type, DocumentConfiguration>();
            Serializer = new DefaultBsonSerializer();
            Logger = new ConsoleLogger(LogLevel.Info, new LoggingColors());
        }

        public ILogger Logger { get; private set; }
        public ISerializer Serializer { get; private set; }

        public IEnumerable<DocumentConfiguration> Tables
        {
            get { return tables.Values; }
        }

        public void Register(DocumentConfiguration association)
        {
            tables.TryAdd(association.Type, association);
        }

        public DocumentConfiguration<T> GetSchemaFor<T>()
        {
            return (DocumentConfiguration<T>) GetSchemaFor(typeof(T));
        }

        public DocumentConfiguration GetSchemaFor(Type type)
        {
            DocumentConfiguration table;
            if (!tables.TryGetValue(type, out table))
                throw new TableNotFoundException(type);

            return table;
        }

        public string GetTableNameByConventionFor<TEntity>()
        {
            return Inflector.Inflector.Pluralize(typeof(TEntity).Name);
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