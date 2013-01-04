using System.Linq;

namespace HybridDb.Linq
{
    public interface IHybridQueryable<out T> : IQueryable<T> {}
}