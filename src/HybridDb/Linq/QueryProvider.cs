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
        readonly IDocumentStore store;
        readonly DocumentSession session;
        readonly QueryStats lastQueryStats;

        public QueryProvider(DocumentSession session)
        {
            this.session = session;
            store = session.Advanced.DocumentStore;
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
            {
                throw new ArgumentException("Query is not based on IEnumerable");
            }

            try
            {
                return (IQueryable) Activator.CreateInstance(typeof (Query<>).MakeGenericType(elementType), new object[] {this, expression});
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        public IEnumerable<TProjection> ExecuteEnumerable<TProjection>(Expression expression)
        {
            return ExecuteQuery<TProjection>(expression).Results;
        }

        T IQueryProvider.Execute<T>(Expression expression)
        {
            var result = ExecuteQuery<T>(expression);

            switch (result.Translation.ExecutionMethod)
            {
                case Translation.ExecutionSemantics.Single:
                    if (lastQueryStats.TotalResults > 1)
                        throw new InvalidOperationException("Query returned more than one element");

                    if (lastQueryStats.TotalResults < 1)
                        throw new InvalidOperationException("Query returned no elements");

                    return result.Results.Single();
                case Translation.ExecutionSemantics.SingleOrDefault:
                    if (lastQueryStats.TotalResults > 1)
                        throw new InvalidOperationException("Query returned more than one element");

                    if (lastQueryStats.TotalResults < 1)
                        return default(T);

                    return result.Results.Single();
                case Translation.ExecutionSemantics.First:
                    if (lastQueryStats.TotalResults < 1)
                        throw new InvalidOperationException("Query returned no elements");

                    return result.Results.First();
                case Translation.ExecutionSemantics.FirstOrDefault:
                    if (lastQueryStats.TotalResults < 1)
                        return default(T);

                    return result.Results.First();
                default:
                    throw new ArgumentOutOfRangeException("Does not support execution method " + result.Translation.ExecutionMethod);
            }
        }

        TranslationAndResult<TProjection> ExecuteQuery<TProjection>(Expression expression)
        {
            var design = store.Configuration.TryGetDesignFor<TSourceElement>();
            var translation = expression.Translate();

            if (typeof (TSourceElement) == typeof (TProjection))
            {
                QueryStats storeStats;
                var results = store.Query(
                    design.Table, out storeStats,
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
                return new TranslationAndResult<TProjection>(translation, results);
            }
            else
            {
                var table = (Table) design.Table;

                QueryStats storeStats;
                var results = store.Query<TProjection>(
                    table, out storeStats,
                    translation.Select,
                    translation.Where,
                    translation.Skip,
                    translation.Take,
                    translation.OrderBy,
                    translation.Parameters);

                storeStats.CopyTo(lastQueryStats);
                return new TranslationAndResult<TProjection>(translation, results);
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

        public object Execute(Expression expression)
        {
            throw new NotSupportedException();
        }

        class TranslationAndResult<T>
        {
            public TranslationAndResult(Translation translation, IEnumerable<T> results)
            {
                Translation = translation;
                Results = results;
            }

            public Translation Translation { get; private set; }
            public IEnumerable<T> Results { get; private set; }
        }
    }
}