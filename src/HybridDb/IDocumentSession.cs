using System;
using System.Linq;
using System.Threading.Tasks;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        Task<T> LoadAsync<T>(string key) where T : class;
        Task<object> LoadAsync(Type type, string key);
        IQueryable<T> Query<T>() where T : class;
        void Store(object entity);
        void Store(string key, object entity);
        void Delete(object entity);
        Task SaveChangesAsync();
        Task SaveChangesAsync(bool lastWriteWins, bool forceWriteUnchangedDocument);
        IAdvancedDocumentSessionCommands Advanced { get; }
    }
}