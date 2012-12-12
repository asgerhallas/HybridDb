using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HybridDb
{
    public class Schema
    {
        readonly Dictionary<Type, ITable> tables;

        public Schema()
        {
            tables = new Dictionary<Type, ITable>();
        }

        public Dictionary<Type, ITable> Tables
        {
            get { return tables; }
        }

        public Table<TEntity> Register<TEntity>()
        {
            var entity = new Table<TEntity>(new JsonSerializer());
            tables.Add(typeof (TEntity), entity);
            return entity;
        }

        public ITable GetTable<T>()
        {
            return tables[typeof (T)];
        }
    }
}