using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace HybridDb.Queue
{
    public class MessageQueueOptions
    {
        public Version Version { get; set; } = new Version(1, 0);
        public TimeSpan IdleDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan ExceptionBackoff { get; set; } = TimeSpan.FromSeconds(15);
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(1);
        public int MaxConcurrency { get; set; } = 4;
        public string TableName { get; set; } = "messages";
        
        public List<string> InboxTopics { get; set; } = new() { EnqueueCommand.DefaultTopic };

        public Func<IDocumentStore, IDocumentSession> CreateSession { get; set; } = store => store.OpenSession();
        public Func<CancellationTokenSource> GetCancellationTokenSource { get; set; } = () => new CancellationTokenSource();
        public Func<IObservable<IHybridDbQueueEvent>, IDisposable> Subscribe { get; set; } = events => Disposable.Empty;
        public ReplaySubject<IHybridDbQueueEvent> Replay { get; set; }

        public MessageQueueOptions ReplayEvents(TimeSpan window)
        {
            Replay = new ReplaySubject<IHybridDbQueueEvent>(window);
            return this;
        }
    }
}