using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb
{
    public static class RowEx
    {
        public static TValue Get<TValue>(this IDictionary<string, object> row, string name) => (TValue) row[name];

        public static TValue Get<TValue>(this IDictionary<string, object> row, Column<TValue> column) => (TValue) row[column.Name];

        public static void Set<TValue>(this IDictionary<string, object> row, Column<TValue> column, TValue value) => row[column.Name] = value;
    }
}