using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb.Linq
{
    public interface IHybridQueryProvider : IQueryProvider
    {
        object Execute<T>(IQueryable<T> query);
        string GetQueryText(IQueryable expression);
    }

    public class QueryProvider<TSourceElement> : IHybridQueryProvider
    {
        readonly DocumentSession session;
        QueryStats stats;

        public QueryProvider(DocumentSession session)
        {
            this.session = session;
            stats = new QueryStats();
        }

        public IQueryable<T> CreateQuery<T>(Expression expression)
        {
            return new Query<T>(this, expression);
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = TypeSystem.GetElementType(expression.Type);
            try
            {
                return (IQueryable) Activator.CreateInstance(typeof (Query<>).MakeGenericType(elementType), new object[] {this, expression});
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        public object Execute<T>(IQueryable<T> query)
        {
            var translation = query.Translate();
            var store = session.Advanced.DocumentStore;
            var table = store.Configuration.GetTableFor(typeof (TSourceElement));

            QueryStats storeStats;
            var results = typeof(TSourceElement) == typeof(T)
                          ? (IEnumerable) store.Query(table, out storeStats, translation.Select, translation.Where, translation.Skip, translation.Take, translation.OrderBy)
                                              .Select(result => session.ConvertToEntityAndPutUnderManagement(table, result))
                          : store.Query<T>(table, out storeStats, translation.Select, translation.Where, translation.Skip, translation.Take, translation.OrderBy);

            storeStats.CopyTo(stats);
            return results;
        }

        public string GetQueryText(IQueryable query)
        {
            return query.Translate().Where;
        }

        internal void WriteStatisticsTo(out QueryStats stats)
        {
            stats = this.stats;
        }

        T IQueryProvider.Execute<T>(Expression expression)
        {
            throw new NotSupportedException();
        }

        object IQueryProvider.Execute(Expression expression)
        {
            throw new NotSupportedException();
        }
    }
}