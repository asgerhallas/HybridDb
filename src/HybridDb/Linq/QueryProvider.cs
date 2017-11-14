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
            var parser = new QueryParser(type => session.DocumentStore.Configuration.TryGetDesignFor(type).Table.Name, name => null);
            var parseResult = parser.Parse(expression);

            return ExecuteQuery<TProjection>(parseResult.ProjectAs == null, parseResult.Statement);
        }

        public T Execute<T>(Expression expression)
        {
            var parser = new QueryParser(type => session.DocumentStore.Configuration.TryGetDesignFor(type).Table.Name, name => null);
            var parseResult = parser.Parse(expression);

            var result = ExecuteQuery<T>(parseResult.ProjectAs == null, parseResult.Statement);

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

        public IEnumerable<TProjection> ExecuteQuery<TProjection>(bool manageAsEntity, SelectStatement statement)
        {
            // hvis select indeholder en column, der svarer til en parameter, så skal den antages som et dokument
            // parameter ender nok med at blive et TableName, så det svarer til at selecte hele tabellen

            if (manageAsEntity)
            {
                QueryStats storeStats;
                var results =
                    from row in store.Query<object>(statement, out storeStats)
                    let concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(design, row.Discriminator)
                    // TProjection is always an entity type (if ProjectAs == null).
                    // Either it's the same as TSourceElement or it is filtered by OfType<TProjection>
                    // but that is still just a filter, not a conversion
                    where typeof (TProjection).IsAssignableFrom(concreteDesign.DocumentType)
                    let entity = session.ConvertToEntityAndPutUnderManagement(concreteDesign, (IDictionary<string, object>) row.Data)
                    where entity != null
                    select (TProjection) entity;
  
                storeStats.CopyTo(lastQueryStats);
                return results;
            }
            else
            {
                QueryStats storeStats;
                var results =
                    from row in store.Query<TProjection>(statement, out storeStats)
                    let concreteDesign = store.Configuration.GetOrCreateDesignByDiscriminator(design, row.Discriminator)
                    //TODO: OfType won't work with this. Go figure it out later.
                    where design.DocumentType.IsAssignableFrom(concreteDesign.DocumentType)
                    select row.Data;

                storeStats.CopyTo(lastQueryStats);
                return results;
            }
        }

        //TODO: this is document store specific and should be moved... 
        public SqlStatementFragments GetQueryText(Expression expression)
        {
            var parser = new QueryParser(type => session.DocumentStore.Configuration.TryGetDesignFor(type).Table.Name, name => null);
            var result = parser.Parse(expression);

            var emitter = new SqlStatementEmitter(x => $"[{x}]", x => x);
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