using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public static class LinqEx
    {
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            if (keyValuePairs == null)
                return null;

            return keyValuePairs.ToDictionary(x => x.Key, x => x.Value);
        }
    }
}