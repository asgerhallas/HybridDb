using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
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
        readonly ConcurrentDictionary<string, int> retries = new();
        readonly ConcurrentDictionary<DocumentTransaction, int> txs = new();
        readonly Subject<IHybridDbQueueEvent> events = new();

        readonly IDocumentStore store;
        readonly ILogger logger;
        readonly QueueTable table;
        readonly MessageQueueOptions options;
        readonly IDisposable eventsSubscription;
        readonly Func<IDocumentSession, HybridDbMessage, Task> handler;
        readonly DedicatedThreadScheduler readerScheduler;
        readonly DedicatedThreadScheduler handlerScheduler;

        public Task MainLoop { get; }
        public IObservable<IHybridDbQueueEvent> Events { get; }

        public HybridDbMessageQueue(
            IDocumentStore store, 
            Func<IDocumentSession, HybridDbMessage, Task> handler)
        {
            this.store = store;
            this.handler = handler;

            if (!store.Configuration.TryResolve(out options))
            {
                throw new HybridDbException("MessageQueue is not enabled. Please run UseMessageQueue in the configuration.");
            }

            Events = options.ObserveEvents(events);

            eventsSubscription = options.SubscribeEvents(Events);

            logger = store.Configuration.Logger;
            table = store.Configuration.Tables.Values.OfType<QueueTable>().Single();

            cts = options.GetCancellationTokenSource();
            readerScheduler = new DedicatedThreadScheduler();
            handlerScheduler = new DedicatedThreadScheduler();

            MainLoop = Task.Factory
                .StartNew(async () =>
                {
                    events.OnNext(new QueueStarting());

                    logger.LogInformation($@"
                        Queue started. 
                        Reading messages with version {options.Version} or older, 
                        from topics '{string.Join("', '", options.InboxTopics)}'.
                    ".Indent());

                    using var semaphore = new SemaphoreSlim(options.MaxConcurrency);

                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var release = await WaitAsync(semaphore);
                                
                            try
                            {
                                var (tx, message) = await NextMessage();

                                try
                                {
                                    await Task.Factory.StartNew(async () =>
                                    {
                                        try
                                        {
                                            using var _ = Time("HandleMessage");

                                            await HandleMessage(tx, message);
                                        }
                                        finally
                                        {
                                            // open the gate for the next message, when this message is handled.
                                            release();
                                            DisposeTransaction(tx);
                                        }
                                    }, cts.Token, TaskCreationOptions.DenyChildAttach, handlerScheduler);
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
                            events.OnNext(new QueueFailed(exception));

                            logger.LogError(exception, $"{nameof(HybridDbMessageQueue)} failed. Will retry.");

                            await Task.Delay(TimeSpan.FromSeconds(15), cts.Token);
                        }
                    }
                    
                    DisposeAllTransactions();

                    logger.LogInformation($"{nameof(HybridDbMessageQueue)} stopped.");
                },
                cts.Token,
                TaskCreationOptions.LongRunning,
                readerScheduler)
            .Unwrap()
            .ContinueWith(
                t => logger.LogError(t.Exception, $"{nameof(HybridDbMessageQueue)} failed and stopped."), 
                TaskContinuationOptions.OnlyOnFaulted);
        }

        DocumentTransaction BeginTransaction()
        {
            cts.Token.ThrowIfCancellationRequested();

            var tx = store.BeginTransaction(timeout: 1);

            if (!txs.TryAdd(tx, 0))
                throw new InvalidOperationException("Transaction could not be tracked.");

            return tx;
        }

        void DisposeTransaction(DocumentTransaction tx)
        {
            if (!txs.TryRemove(tx, out _)) return;

            try
            {
                tx.Dispose();
            }
            catch(Exception ex)
            {
                logger.LogWarning(ex, "Dispose transaction failed.");
            }
        }

        void DisposeAllTransactions()
        {
            foreach (var tx in txs) DisposeTransaction(tx.Key);
        }

        async Task<Action> WaitAsync(SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync(cts.Token);

            var released = 0;
            
            return () =>
            {
                if (Interlocked.Exchange(ref released, 1) == 1) return;
                
                if (cts.IsCancellationRequested) return;

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
                    var message = tx.Execute(new DequeueCommand(table, options.InboxTopics));

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

        async Task HandleMessage(DocumentTransaction tx, HybridDbMessage message)
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
                if (cts.IsCancellationRequested) return;

                var failures = retries.AddOrUpdate(message.Id, key => 1, (key, current) => current + 1);

                events.OnNext(new MessageFailed(context, message, exception, failures));

                if (failures < 5)
                {
                    logger.LogWarning(exception, "Dispatch of command {commandId} failed. Will retry.", message.Id);

                    return;
                }

                logger.LogError(exception, "Dispatch of command {commandId} failed 5 times. Marks command as failed. Will not retry.", message.Id);

                tx.Execute(new EnqueueCommand(table, message with { Topic = $"errors/{message.Topic}" }));

                tx.Complete();

                events.OnNext(new PoisonMessage(context, message, exception));

                retries.TryRemove(message.Id, out _);
            }
        }

        public void Dispose()
        {
            cts.Cancel();

            try
            {
                using var _ = Time("dispose");

                MainLoop.ContinueWith(x => x).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            DisposeAllTransactions();

            eventsSubscription.Dispose();

            handlerScheduler.Dispose();
            readerScheduler.Dispose();
        }

        public Task AwaitShutdown()
        {
            if (!cts.IsCancellationRequested) throw new InvalidOperationException();
            
            return MainLoop;
        }

        public IDisposable Time(string text)
        {
            var startNew = Stopwatch.StartNew();

            return Disposable.Create(() =>
            {
                Debug.WriteLine($"{text}: " + startNew.ElapsedMilliseconds);
            });
        }
    }

    public sealed record HybridDbMessage(string Id, object Payload, string Topic = null, Dictionary<string, string> Metadata = null)
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