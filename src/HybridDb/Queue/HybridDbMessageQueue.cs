using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Indentional;
using Microsoft.Extensions.Logging;

namespace HybridDb.Queue
{
    public class HybridDbMessageQueue : IDisposable
    {
        // This implementation misses a few pieces:
        // [ ] Handling of messages could be idempotent too, by soft deleting them when done, but still keeping it around to guard against
        //     subsequent redelivery with the same id.
        // [ ] Allow faster handling of messages by handling multiple messages (a given max batch size) in one transaction

        readonly CancellationTokenSource cts;
        readonly Stack<IDisposable> disposables = new();
        readonly Subject<IHybridDbQueueEvent> events = new();
        readonly Func<IDocumentSession, HybridDbMessage, Task> handler;
        readonly ILogger logger;
        readonly MessageQueueOptions options;
        readonly ConcurrentDictionary<string, int> retries = new();

        readonly IDocumentStore store;
        readonly QueueTable table;
        readonly ConcurrentDictionary<DocumentTransaction, int> txs = new();
        Stopwatch stopwatch;

        public HybridDbMessageQueue(
            IDocumentStore store,
            Func<IDocumentSession, HybridDbMessage, Task> handler)
        {
            this.store = store;
            this.handler = handler;

            if (!store.Configuration.TryResolve(out options))
            {
                throw new HybridDbException(
                    "MessageQueue is not enabled. Please run UseMessageQueue in the configuration.");
            }

            Events = events;

            if (options.Replay != null)
            {
                disposables.Push(events.Subscribe(options.Replay));

                ReplayedEvents = options.Replay;
            }

            disposables.Push(options.Subscribe(events));

            logger = store.Configuration.Logger;
            table = store.Configuration.Tables.Values.OfType<QueueTable>().Single();

            cts = options.GetCancellationTokenSource();

            id = NextId;

            MainLoop = Task.Factory.StartNew(async () =>
                    {
                        stopwatch = new Stopwatch();

                        stopwatch.Start();

                        events.OnNext(new QueueStarting());

                        logger.LogInformation($@"
                            Queue started. 
                            Reading messages with version {options.Version} or older, 
                            from topics '{string.Join("', '", options.InboxTopics)}'.
                        ".Indent());

                        using var semaphore = new SemaphoreSlim(options.MaxConcurrency);

                        while (!cts.Token.IsCancellationRequested)
                            try
                            {
                                Action release;
                                using (new TimeOperation($"[INFORMATION] Queue #{id} WaitAsync"))
                                {
                                    release = await WaitAsync(semaphore);
                                }

                                try
                                {
                                    var (tx, message) = ((DocumentTransaction)null, (HybridDbMessage)null);
                                    using (new TimeOperation($"[INFORMATION] Queue #{id} NextMessage"))
                                    {
                                        (tx, message) = await NextMessage();
                                    }

                                    try
                                    {
                                        await Task.Factory.StartNew(async () =>
                                        {
                                            try
                                            {
                                                using (new TimeOperation($"[INFORMATION] Queue #{id} HandleMessage"))
                                                {
                                                    await HandleMessage(cts.Token, tx, message);
                                                }
                                            }
                                            finally
                                            {
                                                // open the gate for the next message, when this message is handled.
                                                release();
                                                DisposeTransaction(tx);
                                            }
                                        }, cts.Token, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                                    }
                                    catch
                                    {
                                        release();
                                        DisposeTransaction(tx);
                                        throw;
                                    }
                                }
                                catch
                                {
                                    release();
                                    throw;
                                }
                            }
                            catch (TaskCanceledException) { }
                            catch (Exception exception)
                            {
                                if (!cts.IsCancellationRequested)
                                {
                                    events.OnNext(new QueueFailed(exception));

                                    logger.LogError(exception, $"{nameof(HybridDbMessageQueue)} failed. Will retry.");

                                }
                            }

                        foreach (var tx in txs) DisposeTransaction(tx.Key);

                        logger.LogInformation($"{nameof(HybridDbMessageQueue)} stopped. Ran for {stopwatch.ElapsedMilliseconds} ms.");
                        Debug.WriteLine($"{nameof(HybridDbMessageQueue)} stopped. Ran for {stopwatch.ElapsedMilliseconds} ms.");
                    },
                    cts.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default)
                .Unwrap()
                .ContinueWith(
                    t => logger.LogError(t.Exception, $"{nameof(HybridDbMessageQueue)} failed and stopped."),
                    TaskContinuationOptions.OnlyOnFaulted);
        }

        public Task MainLoop { get; }
        public IObservable<IHybridDbQueueEvent> Events { get; }
        public IObservable<IHybridDbQueueEvent> ReplayedEvents { get; } = Observable.Create<IHybridDbQueueEvent>(ThrowOnSubscribe);

        public void Dispose()
        {
            logger.LogInformation($"{nameof(HybridDbMessageQueue)} disposed. Ran for {stopwatch.ElapsedMilliseconds} ms.");
            Debug.WriteLine($"{nameof(HybridDbMessageQueue)} disposed. Ran for {stopwatch.ElapsedMilliseconds} ms.");

            cts.Cancel();
            // The MainLoop will continue to run until completion.
            // However, we do not wait for it to complete.
            /*try
            {
                MainLoop.Wait();
            }
            catch (TaskCanceledException) { }
            catch (AggregateException ex) when (ex.InnerException is TaskCanceledException) { }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"{nameof(HybridDbMessageQueue)} threw an exception during dispose.");
            }*/

            foreach (var tx in txs) DisposeTransaction(tx.Key);
            foreach (var disposable in disposables) disposable.Dispose();
        }

        static Action ThrowOnSubscribe(IObserver<IHybridDbQueueEvent> _)
        {
            throw new InvalidOperationException("You must set MessageQueueOptions.Replay if you want to subscribe to replayed events.");
        }

        DocumentTransaction BeginTransaction()
        {
            var tx = store.BeginTransaction();

            if (!txs.TryAdd(tx, 0))
            {
                throw new InvalidOperationException("Transaction could not be tracked.");
            }

            return tx;
        }

        void DisposeTransaction(DocumentTransaction tx)
        {
            if (!txs.TryRemove(tx, out _)) return;

            try
            {
                tx.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dispose transaction failed.");
            }
        }

        static async Task<Action> WaitAsync(SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();

            var released = 0;

            return () =>
            {
                if (Interlocked.Exchange(ref released, 1) == 1) return;

                semaphore.Release();
            };
        }

        async Task<(DocumentTransaction, HybridDbMessage)> NextMessage()
        {
            var tx = BeginTransaction();

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    // querying on the queue is done in same transaction as the subsequent write, and the message is temporarily removed
                    // from the queue while handling it, so other machines/workers won't try to handle it too.
                    HybridDbMessage message;

                    using (new TimeOperation($"[INFORMATION] Queue #{id} DequeueCommand"))
                    {
                        message = tx.Execute(new DequeueCommand(table, options.InboxTopics));
                    }

                    if (message != null) return (tx, message);

                    DisposeTransaction(tx);

                    events.OnNext(new QueueIdle());

                    await Task.Delay(options.IdleDelay, cts.Token).ConfigureAwait(false);

                    tx = BeginTransaction();
                }

                return (tx, await Task.FromCanceled<HybridDbMessage>(cts.Token).ConfigureAwait(false));
            }
            catch
            {
                DisposeTransaction(tx);
                throw;
            }
        }

        async Task HandleMessage(CancellationToken ct, DocumentTransaction tx, HybridDbMessage message)
        {
            var context = new MessageContext(message);

            try
            {
                events.OnNext(new MessageReceived(context, message));

                using var session = options.CreateSession(store);

                session.Advanced.SessionData.Add(MessageContext.Key, context);
                session.Advanced.Enlist(tx);

                events.OnNext(new MessageHandling(session, context, message));

                await handler(session, message);

                events.OnNext(new MessageHandled(session, context, message));

                session.SaveChanges();

                tx.Complete();

                events.OnNext(new MessageCommitted(session, context, message));
            }
            catch (Exception exception)
            {
                var failures = retries.AddOrUpdate(message.Id, _ => 1, (_, current) => current + 1);

                events.OnNext(new MessageFailed(context, message, exception, failures));

                if (failures < 5)
                {
                    logger.LogWarning(exception, "Dispatch of command {commandId} failed. Will retry.", message.Id);

                    return;
                }

                // On cancellation we are possible in a state where is it impossible to update the database (see Dispose).
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                logger.LogError(exception,
                    "Dispatch of command {commandId} failed 5 times. Marks command as failed. Will not retry.",
                    message.Id);

                tx.Execute(new EnqueueCommand(table, message with { Topic = $"errors/{message.Topic}" }));

                tx.Complete();

                events.OnNext(new PoisonMessage(context, message, exception));

                retries.TryRemove(message.Id, out _);
            }
        }

        static int currentId;
        readonly int id;

        public static int NextId => Interlocked.Increment(ref currentId);

        public class TimeOperation : IDisposable
        {
            readonly Stopwatch stopwatch = new();
            readonly string title;

            public TimeOperation(string title)
            {
                this.title = title;
                stopwatch.Start();
                Debug.WriteLine($"{title} started");
            }

            public void Dispose()
            {
                stopwatch.Stop();
                Debug.WriteLine($"{title} stopped. Ran for {stopwatch.ElapsedMilliseconds} ms");
            }
        }
    }

    public sealed record HybridDbMessage(
        string Id,
        object Payload,
        string Topic = null,
        int Order = 0,
        Dictionary<string, string> Metadata = null)
    {
        public const string EnqueuedAtKey = "enqueued-at";
        public const string CorrelationIdsKey = "correlation-ids";

        public Dictionary<string, string> Metadata { get; init; } = Metadata ?? new Dictionary<string, string>();
    }

    public class MessageContext : Dictionary<string, object>
    {
        public const string Key = nameof(MessageContext);

        public MessageContext(HybridDbMessage incomingMessage)
        {
            IncomingMessage = incomingMessage;
        }

        public HybridDbMessage IncomingMessage { get; }
    }
}