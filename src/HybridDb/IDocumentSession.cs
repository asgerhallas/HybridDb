using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        T Load<T>(string key) where T : class;
        object Load(DocumentDesign design, string key);
        IQueryable<T> Query<T>() where T : class;
        void Store(object entity);
        void Store(string key, object entity);
        void Delete(object entity);
        void SaveChanges();
        void SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument);
        IAdvancedDocumentSessionCommands Advanced { get; }
    }
}