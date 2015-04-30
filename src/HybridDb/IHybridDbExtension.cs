using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb
{
    public interface IHybridDbExtension
    {
        void OnRead(Table table, IDictionary<string, object> projections);
    }
}