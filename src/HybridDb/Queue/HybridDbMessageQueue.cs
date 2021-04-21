using System;
using System.Collections.Concurrent;
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
        readonly ReplaySubject<IHybridDbDiagnosticEvent> diagnostics;

        readonly IDocumentStore store;
        readonly ILogger logger;
        readonly QueueTable table;
        readonly MessageQueueOptions options;
        readonly Func<IDocumentSession, HybridDbMessage, Task> handler;

        public Task MainLoop { get; }

        public IObservable<IHybridDbDiagnosticEvent> Diagnostics => diagnostics;

        public HybridDbMessageQueue(IDocumentStore store, Func<IDocumentSession, HybridDbMessage, Task> handler, MessageQueueOptions options = null)
        {
            this.store = store;
            this.handler = handler;
            this.options = options ?? new MessageQueueOptions();

            logger = store.Configuration.Logger;
            table = store.Configuration.Tables.Values.OfType<QueueTable>().Single();

            cts = new CancellationTokenSource();
            diagnostics = new ReplaySubject<IHybridDbDiagnosticEvent>(this.options.DiagnosticsReplayWindow);

            MainLoop = Task.Factory.StartNew(async () =>
                {
                    using var semaphore = new SemaphoreSlim(this.options.MaxConcurrency);

                    logger.LogInformation("Queue started.");

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
                            diagnostics.OnNext(new QueueFailed(exception));

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
                var message = tx.Execute(new DequeueCommand(table));

                if (message != null) return message;

                diagnostics.OnNext(new QueueIdle());

                await Task.Delay(options.IdleDelay, cts.Token).ConfigureAwait(false);
            }

            return await Task.FromCanceled<HybridDbMessage>(cts.Token).ConfigureAwait(false);

        }

        async Task HandleMessage(DocumentTransaction tx, HybridDbMessage message)
        {
            try
            {
                using var session = store.OpenSession(tx);

                diagnostics.OnNext(new MessageHandling(session, message));

                await handler(session, message);

                session.SaveChanges();

                diagnostics.OnNext(new MessageHandled(message));
            }
            catch (Exception exception)
            {
                diagnostics.OnNext(new MessageFailed(message, exception));

                var failures = retries.AddOrUpdate(message.Id, key => 1, (key, current) => current + 1);

                if (failures < 5)
                {
                    logger.LogWarning(exception, "Dispatch of command {commandId} failed. Will retry.", message.Id);

                    return;
                }

                logger.LogError(exception, "Dispatch of command {commandId} failed 5 times. Marks command as failed. Will not retry.", message.Id);

                tx.Execute(new EnqueueCommand(table, message, "errors"));

                diagnostics.OnNext(new PoisonMessage(message, exception));

                retries.TryRemove(message.Id, out _);
            }

            tx.Complete();
        }

        public void Dispose()
        {
            cts.Cancel();
            MainLoop.Wait();
        }
    }

    public abstract record HybridDbMessage(string Id);
    public interface IHybridDbDiagnosticEvent { }

    public record MessageHandling(IDocumentSession Session, HybridDbMessage Message) : IHybridDbDiagnosticEvent;
    public record MessageHandled(HybridDbMessage Message) : IHybridDbDiagnosticEvent;
    public record MessageFailed(HybridDbMessage Message, Exception Exception) : IHybridDbDiagnosticEvent;
    public record PoisonMessage(HybridDbMessage Message, Exception Exception) : IHybridDbDiagnosticEvent;
    public record QueueIdle : IHybridDbDiagnosticEvent;
    public record QueueFailed(Exception Exception) : IHybridDbDiagnosticEvent;
}