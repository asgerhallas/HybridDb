using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace HybridDb.Queue
{
    public class MessageQueueOptions
    {
        public TimeSpan IdleDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public int MaxConcurrency { get; set; } = 4;
        public Func<IDocumentStore, IDocumentSession> CreateSession { get; set; } = store => store.OpenSession();
        public Func<IObservable<IHybridDbQueueEvent>, IObservable<IHybridDbQueueEvent>> ObserveQueue { get; set; } = events => events;
    }
}