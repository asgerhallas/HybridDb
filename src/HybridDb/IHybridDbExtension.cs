using System.Collections.Generic;
using HybridDb.Configuration;

namespace HybridDb
{
    public interface IHybridDbExtension
    {
        void OnRead(Table table, IDictionary<string, object> projections);
    }
}