using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public class DocumentSession : IDocumentSession 
    {
        readonly IDocumentStore store;
        readonly Dictionary<Type, ITableConfiguration> entityConfigurations;
        readonly List<object> entities;

        public DocumentSession(IDocumentStore store, Dictionary<Type, ITableConfiguration> entityConfigurations)
        {
            entities = new List<object>();

            this.store = store;
            this.entityConfigurations = entityConfigurations;
        }

        public T Load<T>(string id)
        {
            throw new System.NotImplementedException();
        }

        public void Store(object entity)
        {
            entities.Add(entity);
        }

        public void Delete(object entity)
        {
            throw new System.NotImplementedException();
        }

        public void SaveChanges()
        {
            foreach (var entity in entities)
            {
                var configuration = entityConfigurations[entity.GetType()];
                store.Insert(configuration, configuration.Columns.ToDictionary(x => x, x => x.GetValue(entity)));
            }
        }

        public void Dispose()
        {
            
        }
    }
}