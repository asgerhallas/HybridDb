using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HybridDb.Linq
{
    public static class EnumerableEx
    {
        public static ReadOnlyCollection<T> ToReadOnlyCollection<T>(this IEnumerable<T> enumerable)
        {
            return new ReadOnlyCollection<T>(enumerable.ToList());
        }

        public static IEnumerable<T> Concat<T>(this IEnumerable<T> enumerable, T item)
        {
            return enumerable.Concat(item.AsEnumerable());
        }

        public static IEnumerable<T> AsEnumerable<T>(this T item)
        {
            yield return item;
        }
    }
}