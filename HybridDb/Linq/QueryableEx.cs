using System.Linq;

namespace HybridDb.Linq
{
    public static class QueryableEx
    {
        public static IQueryable<T> AsProjection<T>(this IQueryable query)
        {
            return query.Provider.CreateQuery<T>(query.OfType<T>().Expression);
        }

        public static IQueryable<T> Statistics<T>(this IQueryable<T> query, out QueryStats stats)
        {
            ((QueryProvider<T>)query.Provider).WriteStatisticsTo(out stats);
            return query;
        }
        
        internal static Translation Translate(this IQueryable query)
        {
            return new QueryTranslator().Translate(query.Expression);
        }
    }
}