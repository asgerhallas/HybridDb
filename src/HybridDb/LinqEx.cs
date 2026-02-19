using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public static class LinqEx
    {
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs) => 
            keyValuePairs?.ToDictionary(x => x.Key, x => x.Value);

        public static IEnumerable<T> Unfold<T>(this T first, Func<T, T> nextSelector)
        {
            var x = first;

            while (true)
            {
                yield return x;

                x = nextSelector(x);

                if (x == null) break;
            }
        }
    }
}