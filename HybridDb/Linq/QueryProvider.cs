using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb.Linq
{
    public interface IHybridQueryProvider : IQueryProvider
    {
        object Execute<T>(IQueryable<T> query);
        string GetQueryText(Expression expression);
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
            var translation = Translate(query.Expression);
            var store = session.Advanced.DocumentStore;
            var table = store.Configuration.GetTableFor(typeof (TSourceElement));

            QueryStats stats;
            if (typeof (TSourceElement) == typeof (T))
            {
                return store.Query(table, out stats, select: translation.Select, where: translation.Where)
                            .Select(result => session.ConvertToEntityAndPutUnderManagement(table, result));
            }

            return store.Query<T>(table, out stats, select: translation.Select, where: translation.Where);
        }

        public string GetQueryText(Expression expression)
        {
            return Translate(expression).Where;
        }

        QueryTranslator.Translation Translate(Expression expression)
        {
            //var partiallyEvaluatedExpression = PartialEvaluator.Eval(expression);
            return new QueryTranslator().Translate(expression);
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