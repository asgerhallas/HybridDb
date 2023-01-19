using System;

namespace HybridDb
{
    public class StoreStats
    {
        public long NumberOfRequests { get; internal set; } = 0;
        public long NumberOfCommands { get; internal set; } = 0;
        public long NumberOfGets { get; internal set; } = 0;
        public long NumberOfQueries { get; internal set; } = 0;

        public Guid LastWrittenEtag { get; internal set; }
    }
}