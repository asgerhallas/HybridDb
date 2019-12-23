using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HybridDb.Config;

namespace HybridDb.Linq.Old
{
    public class QueryProvider : IHybridQueryProvider
    {
        readonly DocumentSession session;
        readonly DocumentDesign design;
        readonly IDocumentStore store;
        readonly QueryStats lastQueryStats;

        public QueryProvider(DocumentSession session, DocumentDesign design)
        {
            this.session = session;
            this.design = design;
            store = session.Advanced.DocumentStore;
            lastQueryStats = new QueryStats();
        }

        public IQueryable<T> CreateQuery<T>(Expression expression) => new Query<T>(this, expression);

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
            var translation = expression.Translate();

            if (translation.ProjectAs == null)
            {
                var (stats, rows) = session.Transactionally(tx => tx.Query<object>(
                    design.Table, translation.Select, translation.Where, translation.Skip,
                    translation.Take, translation.OrderBy, false, translation.Parameters));

                var results =
                    from row in rows
                    let concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(design, row.Discriminator)
                    // TProjection is always an entity type (if ProjectAs == null).
                    // Either it's the same as TSourceElement or it is filtered by OfType<TProjection>
                    // but that is still just a filter, not a conversion
                    where typeof (TProjection).IsAssignableFrom(concreteDesign.DocumentType)
                    let entity = session.ConvertToEntityAndPutUnderManagement(concreteDesign, (IDictionary<string, object>) row.Data)
                    where entity != null
                    select (TProjection) entity;
  
                stats.CopyTo(lastQueryStats);

                return new TranslationAndResult<TProjection>(translation, results);
            }
            else
            {
                var (stats, rows) = session.Transactionally(tx => tx.Query<TProjection>(
                    design.Table, translation.Select, translation.Where, translation.Skip,
                    translation.Take, translation.OrderBy, false, translation.Parameters));

                var results =
                    from row in rows
                    let concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(design, row.Discriminator)
                    //TODO: OfType won't work with this. Go figure it out later.
                    where design.DocumentType.IsAssignableFrom(concreteDesign.DocumentType)
                    select row.Data;

                stats.CopyTo(lastQueryStats);
                return new TranslationAndResult<TProjection>(translation, results);
            }
        }

        public string GetQueryText(IQueryable query) => query.Translate().Where;

        internal void WriteStatisticsTo(out QueryStats stats) => stats = lastQueryStats;

        public object Execute(Expression expression) => throw new NotSupportedException();

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