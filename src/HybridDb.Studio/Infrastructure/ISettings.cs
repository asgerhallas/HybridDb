using System;
using System.Collections.Generic;

namespace HybridDb.Studio.Infrastructure
{
    public interface ISettings
    {
        IReadOnlyList<string> RecentFiles { get; }
        void Save();
    }
}