using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public static class LinqEx
    {
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs) => 
            keyValuePairs?.ToDictionary(x => x.Key, x => x.Value);
    }
}