using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb.Linq
{
    public class QueryProvider : IQueryProvider
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
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        public T Execute<T>(Expression expression)
        {
            return (T) Execute(expression);
        }

        public object Execute(Expression expression)
        {
            var text = Translate(expression);
            var elementType = TypeSystem.GetElementType(expression.Type);
            QueryStats stats;
            var store = session.Advanced.DocumentStore;
            var table = store.Configuration.GetTableFor(elementType);
            
            var results = store.Query(table, out stats, where: text);
            return results.Select(result => session.ConvertToEntityAndPutUnderManagement(table, result));
        }

        public string GetQueryText(Expression expression)
        {
            return Translate(expression);
        }

        string Translate(Expression expression)
        {
            return new QueryTranslator().Translate(expression);
        }
    }
}