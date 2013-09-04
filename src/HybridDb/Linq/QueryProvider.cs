using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HybridDb.Schema;

namespace HybridDb.Linq
{
    public class QueryProvider<TSourceElement> : IHybridQueryProvider where TSourceElement : class
    {
        readonly DocumentSession session;
        readonly QueryStats lastQueryStats;

        public QueryProvider(DocumentSession session)
        {
            this.session = session;
            lastQueryStats = new QueryStats();
        }

        public IQueryable<T> CreateQuery<T>(Expression expression)
        {
            return new Query<T>(this, expression);
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetEnumeratedType();
            if (elementType == null)
                throw new ArgumentException("Query is not based on IEnumerable");

            try
            {
                return (IQueryable) Activator.CreateInstance(typeof (Query<>).MakeGenericType(elementType), new object[] {this, expression});
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        public IEnumerable<TProjection> ExecuteQuery<TProjection>(Translation translation)
        {
            var store = session.Advanced.DocumentStore;
            var design = store.Configuration.TryGetDesignFor<TSourceElement>();

            if (typeof (TSourceElement) == typeof (TProjection) && design != null)
            {
                QueryStats storeStats;
                var results = store.Query(design.Table,
                                          out storeStats,
                                          translation.Select,
                                          translation.Where,
                                          translation.Skip,
                                          translation.Take,
                                          translation.OrderBy,
                                          translation.Parameters)
                    .Select(result => session.ConvertToEntityAndPutUnderManagement(design, result))
                    .Where(result => result != null)
                    .Cast<TProjection>();
  
                storeStats.CopyTo(lastQueryStats);
                return results;
            }
            else
            {
                var table = design != null
                                ? (Table) design.Table
                                : store.Configuration.TryGetIndexTableByType(typeof (TSourceElement));

                QueryStats storeStats;
                var results = store.Query<TProjection>(table,
                                             out storeStats,
                                             translation.Select,
                                             translation.Where,
                                             translation.Skip,
                                             translation.Take,
                                             translation.OrderBy,
                                             translation.Parameters);

                storeStats.CopyTo(lastQueryStats);
                return results;
            }
        }



        public string GetQueryText(IQueryable query)
        {
            return query.Translate().Where;
        }

        internal void WriteStatisticsTo(out QueryStats stats)
        {
            stats = lastQueryStats;
        }

        T IQueryProvider.Execute<T>(Expression expression)
        {
            var translation = expression.Translate();

            var result = ExecuteQuery<T>(translation);

            switch (translation.ExecutionMethod)
            {
                case Translation.ExecutionSemantics.Single:
                    if (lastQueryStats.TotalResults > 1)
                        throw new InvalidOperationException("Query returned more than one element");

                    if (lastQueryStats.TotalResults < 1)
                        throw new InvalidOperationException("Query returned no elements");

                    return result.Single();
                case Translation.ExecutionSemantics.SingleOrDefault:
                    if (lastQueryStats.TotalResults > 1)
                        throw new InvalidOperationException("Query returned more than one element");

                    if (lastQueryStats.TotalResults < 1)
                        return default(T);

                    return result.Single();
                case Translation.ExecutionSemantics.First:
                    if (lastQueryStats.TotalResults < 1)
                        throw new InvalidOperationException("Query returned no elements");

                    return result.First();
                case Translation.ExecutionSemantics.FirstOrDefault:
                    if (lastQueryStats.TotalResults < 1)
                        return default(T);

                    return result.First();
                default:
                    throw new ArgumentOutOfRangeException("Does not support execution method " + translation.ExecutionMethod);
            }
        }

        public object Execute(Expression expression)
        {
            throw new NotSupportedException();
        }
    }
}