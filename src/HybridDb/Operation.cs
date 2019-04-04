using System;

namespace HybridDb
{
    [Flags]
    public enum Operation
    {
        Inserted = 1,
        Updated = 2,
        Deleted = 4
    }
}