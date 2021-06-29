using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace HybridDb.Queue
{
    public class MessageQueueOptions
    {
        public TimeSpan IdleDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public int MaxConcurrency { get; set; } = 4;
        public Func<IDocumentStore, IDocumentSession> CreateSession { get; set; } = store => store.OpenSession();
        public Func<CancellationTokenSource> GetCancellationTokenSource { get; set; } = () => new CancellationTokenSource();
        public Func<IObservable<IHybridDbQueueEvent>, IObservable<IHybridDbQueueEvent>> ObserveEvents { get; set; } = events => events;
        public Func<IObservable<IHybridDbQueueEvent>, IDisposable> SubscribeEvents { get; set; } = events => Disposable.Empty;

        public MessageQueueOptions ReplayEvents(TimeSpan window)
        {
            ObserveEvents = Compose(ObserveEvents, o =>
            {
                var replay = o.Replay(window);

                replay.Connect();

                return replay;
            });

            return this;
        }

        public Func<T, T> Compose<T>(Func<T, T> a, Func<T, T> b) => x => b(a(x));
    }
}