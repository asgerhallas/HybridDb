using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography.X509Certificates;
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

            MainLoop = Task.Factory.StartNew(async () =>
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
                                                await HandleMessage(tx, message);
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
                            catch (TaskCanceledException)
                            {
                            }
                            catch (Exception exception)
                            {
                                events.OnNext(new QueueFailed(exception));

                                logger.LogError(exception, $"{nameof(HybridDbMessageQueue)} failed. Will retry.");
                            }
                        }

                        foreach (var tx in txs) DisposeTransaction(tx.Key);

                        logger.LogInformation($"{nameof(HybridDbMessageQueue)} stopped.");
                    },
                    cts.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default)
                .Unwrap()
                .ContinueWith(
                    t => logger.LogError(t.Exception, $"{nameof(HybridDbMessageQueue)} failed and stopped."), 
                    TaskContinuationOptions.OnlyOnFaulted);
        }

        DocumentTransaction BeginTransaction()
        {
            var tx = store.BeginTransaction();

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

        async Task<Action> WaitAsync(SemaphoreSlim semaphore)
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
            var context = new MessageContext();

            try
            {
                events.OnNext(new MessageHandling(context, message));

                using var session = options.CreateSession(store);

                session.Advanced.Enlist(tx);

                await handler(session, message);

                session.SaveChanges();

                events.OnNext(new MessageHandled(session, context, message));
            }
            catch (Exception exception)
            {
                events.OnNext(new MessageFailed(context, message, exception));

                var failures = retries.AddOrUpdate(message.Id, key => 1, (key, current) => current + 1);

                if (failures < 5)
                {
                    logger.LogWarning(exception, "Dispatch of command {commandId} failed. Will retry.", message.Id);

                    return;
                }

                logger.LogError(exception, "Dispatch of command {commandId} failed 5 times. Marks command as failed. Will not retry.", message.Id);

                tx.Execute(new EnqueueCommand(table, message with { Topic = $"errors/{message.Topic}" }));

                events.OnNext(new PoisonMessage(context, message, exception));

                retries.TryRemove(message.Id, out _);
            }

            tx.Complete();
        }

        public void Dispose()
        {
            cts.Cancel();
            
            try { MainLoop.Wait(); }
            catch (TaskCanceledException) { }
            catch (AggregateException ex) when (ex.InnerException is TaskCanceledException) { }
            catch (Exception ex) { logger.LogWarning(ex, $"{nameof(HybridDbMessageQueue)} threw an exception during dispose."); }

            eventsSubscription.Dispose();
        }
    }

    public sealed record HybridDbMessage(string Id, object Payload, string Topic = null, Func<object, Guid, string> IdGenerator = null)
    {
        public Dictionary<string, string> Metadata { get; } = new();
    }
    
    public class MessageContext : Dictionary<string, object> { }
}