using System;
using HybridDb.Config;

namespace HybridDb
{
    /// <summary>
    /// Represents a global identification of a document consisting of both type and ID
    /// </summary>
    public class EntityKey : Tuple<Table, string>
    {
        public EntityKey(Table table, string id) : base(table, id) {}

        public Table Table => Item1;
        public string Id => Item2;
    }
}