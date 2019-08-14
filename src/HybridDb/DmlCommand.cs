using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb
{
    public abstract class DmlCommand
    {
        public static IDictionary<Column, object> ConvertAnonymousToProjections(Table table, object projections) =>
            projections as IDictionary<Column, object> ?? (
                from projection in projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections)
                let column = table[projection.Key]
                where column != null
                select new KeyValuePair<Column, object>(column, projection.Value)
            ).ToDictionary();

    }

    public abstract class Command<TResult> : DmlCommand { }
}