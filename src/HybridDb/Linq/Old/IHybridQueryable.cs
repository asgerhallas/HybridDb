using System.Linq;

namespace HybridDb.Linq.Old
{
    public interface IHybridQueryable<out T> : IQueryable<T>
    {
    }
}