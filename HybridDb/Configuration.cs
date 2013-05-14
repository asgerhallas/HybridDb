using System;
using System.Collections.Generic;
using HybridDb.Logging;
using HybridDb.Schema;

namespace HybridDb
{
    public class Conventions
    {
    }

    public class Configuration
    {
        readonly Dictionary<Type, Table> tables;

        public Configuration()
        {
            tables = new Dictionary<Type, Table>();
            Serializer = new DefaultBsonSerializer();
            Logger = new ConsoleLogger(LogLevel.Info, new LoggingColors());
        }

        public ILogger Logger { get; private set; }
        public ISerializer Serializer { get; private set; }

        public Dictionary<Type, Table> Tables
        {
            get { return tables; }
        }

        public TableBuilder<TEntity> Register<TEntity>(string name)
        {
            var table = new Table(name ?? GetTableNameByConventionFor<TEntity>());
            tables.Add(typeof (TEntity), table);
            return new TableBuilder<TEntity>(table);
        }

        public string GetTableNameByConventionFor<TEntity>()
        {
            return Inflector.Inflector.Pluralize(typeof(TEntity).Name);
        }

        public void UseSerializer(ISerializer serializer)
        {
            Serializer = serializer;
        }

        public void UseSerializer(ILogger logger)
        {
            Logger = logger;
        }

        public Table GetTableFor<T>()
        {
            return GetTableFor(typeof (T));
        }

        public Table GetTableFor(Type type)
        {
            Table value;
            if (tables.TryGetValue(type, out value))
                return value;

            throw new TableNotFoundException(type);
        }
    }
}