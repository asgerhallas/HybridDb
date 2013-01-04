using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        T Load<T>(Guid id) where T : class;
        IEnumerable<T> Query<T>(string @where, object parameters) where T : class;
        IEnumerable<TProjection> Query<T, TProjection>(string where, object parameters) where T : class;
        IQueryable<T> Query<T>() where T : class;
        void Store(object entity);
        void Delete(object entity);
        void SaveChanges();
        IAdvancedDocumentSessionCommands Advanced { get; }
    }
}