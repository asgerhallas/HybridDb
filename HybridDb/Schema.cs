using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HybridDb
{
    public class Schema
    {
        readonly Dictionary<Type, ITableConfiguration> tables;

        public Schema()
        {
            tables = new Dictionary<Type, ITableConfiguration>();
        }

        public Dictionary<Type, ITableConfiguration> Tables
        {
            get { return tables; }
        }

        public TableConfiguration<TEntity> Register<TEntity>()
        {
            var entity = new TableConfiguration<TEntity>(new JsonSerializer());
            tables.Add(typeof (TEntity), entity);
            return entity;
        }

        public ITableConfiguration GetTable<T>()
        {
            return tables[typeof (T)];
        }
    }
}