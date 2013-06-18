using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Linq
{
    public interface IHybridQueryProvider : IQueryProvider
    {
        IEnumerable<T> ExecuteQuery<T>(Translation translation);
        string GetQueryText(IQueryable expression);
    }
}