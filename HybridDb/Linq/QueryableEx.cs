using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Linq.Ast;

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

        public static bool In<T>(this T property, params T[] list)
        {
            return list.Any(item => property.Equals(item));
        }
        
        internal static Translation Translate(this IQueryable query)
        {
            var test = new QueryTranslator().Translate(query.Expression);
            return test;
            return new QueryVisitor().Translate(query.Expression);
        }
    }
}