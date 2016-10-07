using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq2;
using HybridDb.Linq2.Emitter;

namespace HybridDb.Linq
{
    public interface IHybridQueryProvider : IQueryProvider
    {
        IEnumerable<T> ExecuteEnumerable<T>(Expression expression);
        SqlStatementFragments GetQueryText(Expression expression);
    }
}