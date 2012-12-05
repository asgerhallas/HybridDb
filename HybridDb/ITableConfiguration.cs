using System.Collections.Generic;

namespace HybridDb
{
    public interface ITableConfiguration
    {
        string Name { get; }
        IEnumerable<IColumnConfiguration> Columns { get; }
        IdColumn IdColumn { get; }
        EtagColumn EtagColumn { get; }
        DocumentColumn DocumentColumn { get; }
        IColumnConfiguration this[string name] { get; }
    }
}