using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public static class Helpers
    {
        public static List<T> ListOf<T>(params T[] list) => list.ToList();
    }
}