using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb
{
    /// <summary>
    /// Base class for data modification commands that can be issues by the DocumentStore.
    /// </summary>
    public abstract class HybridDbCommand
    {
        public static IDictionary<Column, object> ConvertAnonymousToProjections(Table table, object projections) =>
            projections as IDictionary<Column, object> ?? (
                from projection in projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections)
                let column = table[projection.Key]
                where column != null
                select new KeyValuePair<Column, object>(column, projection.Value)
            ).ToDictionary();

    }

    public abstract class HybridDbCommand<TResult> : HybridDbCommand { }
}