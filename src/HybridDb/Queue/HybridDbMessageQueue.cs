using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
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
            Func<IDocumentSession, HybridDbMessage, Task> handler, 
            MessageQueueOptions options = null)
        {
            this.store = store;
            this.handler = handler;
            this.options = options ?? new MessageQueueOptions();

            Events = this.options.ObserveEvents(events);

            eventsSubscription = this.options.SubscribeEvents(Events);

            logger = store.Configuration.Logger;
            table = store.Configuration.Tables.Values.OfType<QueueTable>().Single();

            cts = this.options.GetCancellationTokenSource();

            MainLoop = Task.Factory.StartNew(async () =>
                {
                    events.OnNext(new QueueStarting());

                    logger.LogInformation("Queue started.");

                    using var semaphore = new SemaphoreSlim(this.options.MaxConcurrency);

                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var release = await WaitAsync(semaphore);

                            var tx = BeginTransaction();

                            try
                            {
                                var message = await NextMessage(tx);

                                await Task.Factory.StartNew(async () =>
                                {
                                    try { await HandleMessage(tx, message); }
                                    finally
                                    {
                                        release();
                                        DisposeTransaction(tx);
                                    }
                                }, cts.Token);
                            }
                            catch
                            {
                                release();
                                DisposeTransaction(tx);
                                throw;
                            }
                        }
                        catch (TaskCanceledException) { }
                        catch (Exception exception)
                        {
                            events.OnNext(new QueueFailed(exception));

                            logger.LogError(exception, $"{nameof(HybridDbMessageQueue)} failed. Will retry.");
                        }
                    }

                    foreach (var tx in txs) DisposeTransaction(tx.Key);

                    logger.LogInformation("Queue stopped.");
                }, 
                cts.Token, 
                TaskCreationOptions.LongRunning, 
                TaskScheduler.Default
            ).Unwrap();
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
            if (!txs.TryRemove(tx, out _))
                throw new InvalidOperationException("Transaction was not tracked.");

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

        async Task<HybridDbMessage> NextMessage(DocumentTransaction tx)
        {
            while (!cts.IsCancellationRequested)
            {
                // querying on the queue is done in same transaction as the subsequent write, and the message is temporarily removed
                // from the queue while handling it, so other machines/workers won't try to handle it too.
                var message = tx.Execute(new DequeueCommand(table, options.InboxTopic));

                if (message != null) return message;

                events.OnNext(new QueueIdle());

                await Task.Delay(options.IdleDelay, cts.Token).ConfigureAwait(false);
            }

            return await Task.FromCanceled<HybridDbMessage>(cts.Token).ConfigureAwait(false);

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

                events.OnNext(new MessageHandled(context, message));
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

                tx.Execute(new EnqueueCommand(table, message, options.ErrorTopic));

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

    public abstract record HybridDbMessage(string Id);
    
    public interface IHybridDbQueueEvent { }

    public record MessageHandling(MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageHandled(MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageFailed(MessageContext Context, HybridDbMessage Message, Exception Exception) : IHybridDbQueueEvent;
    public record PoisonMessage(MessageContext Context, HybridDbMessage Message, Exception Exception) : IHybridDbQueueEvent;
    public record QueueStarting : IHybridDbQueueEvent;
    public record QueueIdle : IHybridDbQueueEvent;
    public record QueueFailed(Exception Exception) : IHybridDbQueueEvent;

    public class MessageContext : Dictionary<string, object> { }
}