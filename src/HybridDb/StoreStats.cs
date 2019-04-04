using System;

namespace HybridDb
{
    public class StoreStats
    {
        public long NumberOfRequests { get; set; }
        public long NumberOfInsertCommands { get; set; } = 0;
        public long NumberOfUpdateCommands { get; set; } = 0;
        public long NumberOfDeleteCommands { get; set; } = 0;
        public long NumberOfGets { get; set; } = 0;
        public long NumberOfQueries { get; set; } = 0;

        public Guid LastWrittenEtag { get; set; }
    }
}