using System;
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

        public QueryProvider(DocumentSession session)
        {
            this.session = session;
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

            QueryStats stats;
            if (typeof (TSourceElement) == typeof (T))
            {
                return store.Query(table, out stats, translation.Select, translation.Where, translation.Skip, translation.Take, translation.OrderBy)
                            .Select(result => session.ConvertToEntityAndPutUnderManagement(table, result));
            }

            return store.Query<T>(table, out stats, translation.Select, translation.Where, translation.Skip, translation.Take, translation.OrderBy);
        }

        public string GetQueryText(IQueryable query)
        {
            return query.Translate().Where;
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