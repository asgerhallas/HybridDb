using System.Linq;

namespace HybridDb.Linq
{
    public static class QueryableEx
    {
        public static IQueryable<T> AsProjection<T>(this IQueryable query)
        {
            return query.Provider.CreateQuery<T>(query.OfType<T>().Expression);
        }

        internal static Translation Translate(this IQueryable query)
        {
            return new QueryTranslator().Translate(query.Expression);
        }
    }
}