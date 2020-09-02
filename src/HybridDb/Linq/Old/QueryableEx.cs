using System;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Old.Parsers;

namespace HybridDb.Linq.Old
{
    public static class QueryableEx
    {
        public static IQueryable<T> AsProjection<T>(this IQueryable query) => query.Provider.CreateQuery<T>(query.OfType<T>().Expression);

        public static IQueryable<T> Statistics<T>(this IQueryable<T> query, out QueryStats stats) where T : class
        {
            ((QueryProvider)query.Provider).WriteStatisticsTo(out stats);
            return query;
        }

        // courtesy implementation - not really necessary for linq provider
        // but i repeatedly ended up using the method in linq-for-objects too.
        public static bool In<T>(this T property, params T[] list) => list.Contains(property);

        public static T Column<T>(this object parameter, string name) => throw new NotSupportedException("Only for building LINQ expressions");

        public static T Index<T>(this object parameter) => throw new NotSupportedException("Only for building LINQ expressions");

        internal static Translation Translate(this IQueryable query) => Translate(query.Expression);

        internal static Translation Translate(this Expression expression) => new QueryTranslator().Translate(expression);
    }
}