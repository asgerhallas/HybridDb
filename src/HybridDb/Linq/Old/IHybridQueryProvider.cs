using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace HybridDb.Linq.Old
{
    public interface IHybridQueryProvider : IQueryProvider
    {
        IEnumerable<T> ExecuteEnumerable<T>(Expression expression);
        string GetQueryText(IQueryable expression);
    }
}