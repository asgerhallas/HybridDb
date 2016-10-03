using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public abstract class DatabaseCommand
    {


        public IDictionary<Column, object> ConvertAnonymousToProjections(Table table, object projections)
        {
            return projections as IDictionary<Column, object> ??
                   (from projection in projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections)
                    let column = table[projection]
                    where column != null
                    select new KeyValuePair<Column, object>(column, projection.Value)).ToDictionary();
        }
    }
}