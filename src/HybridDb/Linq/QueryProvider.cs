using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HybridDb.Config;
using HybridDb.Linq.Parsers;
using HybridDb.Linq2;
using HybridDb.Linq2.Ast;
using HybridDb.Linq2.Emitter;

namespace HybridDb.Linq
{
    public class QueryProvider<TSourceElement> : IHybridQueryProvider where TSourceElement : class
    {
        readonly IDocumentStore store;
        readonly DocumentSession session;
        readonly DocumentDesign design;
        readonly QueryStats lastQueryStats;

        public QueryProvider(DocumentSession session, DocumentDesign design)
        {
            this.session = session;
            this.design = design;
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
                return (IQueryable) Activator.CreateInstance(typeof (Query<>).MakeGenericType(elementType), this, expression);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        public IEnumerable<TProjection> ExecuteEnumerable<TProjection>(Expression expression)
        {
            var parser = new QueryParser();
            var parseResult = parser.Parse(design, expression);

            return ExecuteQuery<TProjection>(parseResult.Statement);
        }

        public T Execute<T>(Expression expression)
        {
            var parser = new QueryParser();
            var parseResult = parser.Parse(design, expression);

            var result = ExecuteQuery<T>(parseResult.Statement);

            switch (parseResult.Execution)
            {
                case Execution.Single:
                    if (lastQueryStats.TotalResults > 1)
                        throw new InvalidOperationException("Query returned more than one element.");

                    if (lastQueryStats.TotalResults < 1)
                        throw new InvalidOperationException("Query returned no elements.");

                    return result.Single();
                case Execution.SingleOrDefault:
                    if (lastQueryStats.TotalResults > 1)
                        throw new InvalidOperationException("Query returned more than one element.");

                    if (lastQueryStats.TotalResults < 1)
                        return default(T);

                    return result.Single();
                case Execution.First:
                    if (lastQueryStats.TotalResults < 1)
                        throw new InvalidOperationException("Query returned no elements.");

                    return result.First();
                case Execution.FirstOrDefault:
                    if (lastQueryStats.TotalResults < 1)
                        return default(T);

                    return result.First();
                default:
                    throw new ArgumentOutOfRangeException("Does not support execution method " + parseResult.Execution);
            }
        }

        public IEnumerable<TProjection> ExecuteQuery<TProjection>(SelectStatement statement)
        {
            if (typeof (TProjection).IsAssignableFrom(design.DocumentType))
            {
                QueryStats storeStats;
                var results = store.Query(statement, out storeStats)
                    .Select(result => session.ConvertToEntityAndPutUnderManagement(design, result))
                    .Where(result => result != null)
                    .Cast<TProjection>();
  
                storeStats.CopyTo(lastQueryStats);
                return results;
            }
            else
            {
                QueryStats storeStats;
                var results = store.Query<TProjection>(statement, out storeStats);

                storeStats.CopyTo(lastQueryStats);
                return results;
            }
        }

        public SqlStatementFragments GetQueryText(Expression expression)
        {
            var parser = new QueryParser();
            var result = parser.Parse(design, expression);

            var emitter = new SqlStatementEmitter();
            var sql = emitter.Emit(result.Statement);

            return sql;
        }

        internal void WriteStatisticsTo(out QueryStats stats)
        {
            stats = lastQueryStats;
        }

        public object Execute(Expression expression)
        {
            throw new NotSupportedException();
        }
    }
}