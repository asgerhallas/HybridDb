using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HybridDb.Linq.Old
{
    public static class EnumerableEx
    {
        internal static ReadOnlyCollection<T> ToReadOnlyCollection<T>(this IEnumerable<T> enumerable)
        {
            return new ReadOnlyCollection<T>(enumerable.ToList());
        }

        internal static IEnumerable<T> Concat<T>(this IEnumerable<T> enumerable, T item)
        {
            return enumerable.Concat(item.AsEnumerable());
        }

        internal static IEnumerable<T> AsEnumerable<T>(this T item)
        {
            yield return item;
        }
    }
}