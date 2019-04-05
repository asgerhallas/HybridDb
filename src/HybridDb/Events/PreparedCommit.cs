using System;
using System.Collections.Generic;

namespace HybridDb.Events
{
    public class PreparedCommit
    {
        public PreparedCommit(Guid commitId, Generation generation, params EventData<byte[]>[] events) 
            : this(commitId, generation, (IReadOnlyList<EventData<byte[]>>)events) { }

        public PreparedCommit(Guid commitId, Generation generation, IReadOnlyList<EventData<byte[]>> events)
        {
            CommitId = commitId;
            Generation = generation;
            Events = events;
        }

        public Guid CommitId { get; }
        public Generation Generation { get; }
        public IReadOnlyList<EventData<byte[]>> Events { get; }
    }
}