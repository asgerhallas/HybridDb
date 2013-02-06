using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using HybridDb.Logging;
using HybridDb.Schema;

namespace HybridDb
{
    public class Conventions
    {
    }

    public class Configuration
    {
        readonly Dictionary<Type, ITable> tables;

        public Configuration()
        {
            tables = new Dictionary<Type, ITable>();
            Serializer = new DefaultBsonSerializer();
            Logger = new ConsoleLogger(LogLevel.Info, new LoggingColors());
        }

        public ILogger Logger { get; private set; }
        public ISerializer Serializer { get; private set; }

        public Dictionary<Type, ITable> Tables
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

        public ITable GetTableFor<T>()
        {
            return GetTableFor(typeof (T));
        }

        public ITable GetTableFor(Type type)
        {
            ITable value;
            if (tables.TryGetValue(type, out value))
                return value;

            throw new TableNotFoundException(type);
        }
    }

    public class TableBuilder<TEntity>
    {
        readonly ITable table;

        public TableBuilder(ITable table)
        {
            this.table = table;
        }

        public TableBuilder<TEntity> Projection<TMember>(Expression<Func<TEntity, TMember>> member)
        {
            var column = new ProjectionColumn<TEntity, TMember>(member);
            table.AddProjection(column);
            return this;
        }
    }
}