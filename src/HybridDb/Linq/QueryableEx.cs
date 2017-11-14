using System;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Config;
using HybridDb.Linq.Parsers;
using HybridDb.Linq2;

namespace HybridDb.Linq
{
    public static class QueryableEx
    {
        public static IQueryable<T> AsProjection<T>(this IQueryable query)
        {
            return query.Provider.CreateQuery<T>(query.OfType<T>().Expression);
        }

        public static IQueryable<T> Statistics<T>(this IQueryable<T> query, out QueryStats stats) where T : class
        {
            ((QueryProvider)query.Provider).WriteStatisticsTo(out stats);
            return query;
        }

        public static bool In<T>(this T property, params T[] list)
        {
            // courtesy implementation - not really neccessary for linq provider
            // but i repeatedly ended up using the method in linq-for-objects too.
            return list.Contains(property);
        }
        
        public static T Column<T>(this object parameter, string name)
        {
            throw new NotSupportedException("Only for building LINQ expressions");
        }
        
        public static T Index<T>(this object parameter)
        {
            throw new NotSupportedException("Only for building LINQ expressions");
        }
    }
}