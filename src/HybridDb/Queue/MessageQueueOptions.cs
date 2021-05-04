using System;

namespace HybridDb.Queue
{
    public class MessageQueueOptions
    {
        public TimeSpan IdleDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan DiagnosticsReplayWindow { get; set; } = TimeSpan.FromSeconds(60);
        public int MaxConcurrency { get; set; } = 4;
        public Func<IDocumentStore, IDocumentSession> CreateSession { get; set; } = store => store.OpenSession();
    }
}