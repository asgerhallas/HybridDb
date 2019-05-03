using System;

namespace HybridDb
{
    public class StoreStats
    {
        public long NumberOfRequests { get; set; } = 0;
        public long NumberOfCommands { get; set; } = 0;
        public long NumberOfGets { get; set; } = 0;
        public long NumberOfQueries { get; set; } = 0;

        public Guid LastWrittenEtag { get; set; }
    }
}