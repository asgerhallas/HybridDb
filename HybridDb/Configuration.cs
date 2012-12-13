using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HybridDb
{
    public class Configuration
    {
        readonly Dictionary<Type, ITable> tables;

        public Configuration()
        {
            tables = new Dictionary<Type, ITable>();
        }

        public Dictionary<Type, ITable> Tables
        {
            get { return tables; }
        }

        public Table<TEntity> Register<TEntity>()
        {
            var entity = new Table<TEntity>();
            tables.Add(typeof (TEntity), entity);
            return entity;
        }

        public ITable GetTableFor<T>()
        {
            return tables[typeof (T)];
        }

        public ITable GetTableFor(Type type)
        {
            return tables[type];
        }

        public ISerializer CreateSerializer()
        {
            return new Serializer();
        }
    }
}