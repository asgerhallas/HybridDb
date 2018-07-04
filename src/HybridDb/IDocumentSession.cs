using System;
using System.Linq;
using System.Threading.Tasks;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        Task<T> Load<T>(string key) where T : class;
        Task<object> Load(Type type, string key);
        IQueryable<T> Query<T>() where T : class;
        void Store(object entity);
        void Store(string key, object entity);
        void Delete(object entity);
        Task SaveChanges();
        Task SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument);
        IAdvancedDocumentSessionCommands Advanced { get; }
    }
}