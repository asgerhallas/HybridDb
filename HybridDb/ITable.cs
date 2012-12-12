using System.Collections.Generic;

namespace HybridDb
{
    public interface ITable
    {
        string Name { get; }
        IEnumerable<IColumn> Columns { get; }
        IdColumn IdColumn { get; }
        EtagColumn EtagColumn { get; }
        DocumentColumn DocumentColumn { get; }
        IColumn this[string name] { get; }
    }
}