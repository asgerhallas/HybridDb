using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb
{
    public interface ISchema
    {
        Dictionary<string, Table> GetSchema();
    }
}