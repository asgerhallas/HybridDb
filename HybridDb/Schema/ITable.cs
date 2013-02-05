using System;
using System.Collections.Generic;

namespace HybridDb.Schema
{
    public interface ITable
    {
        string Name { get; }
        IEnumerable<Column> Columns { get; }
        IdColumn IdColumn { get; }
        EtagColumn EtagColumn { get; }
        DocumentColumn DocumentColumn { get; }
        Column this[string name] { get; }
        Column GetNamedOrDynamicColumn(string name, object value);
        void AddProjection(IProjectionColumn column);
    }
}