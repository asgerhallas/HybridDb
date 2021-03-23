using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Events;
using Microsoft.Extensions.Logging;

namespace HybridDb.Queue
{
    public class HybridDbMessageQueue : IDisposable
    {
        // This implementation misses a few pieces:
        // [ ] Handling of messages could be idempotent too, by soft deleting them when done, but still keeping it around to guard against
        //    subsequent redelivery with the same id.
        // [ ] When Idle it should use a less intensive peek, instead of dequeue

        readonly CancellationTokenSource cts;
        readonly ConcurrentDictionary<string, int> retries = new();
        readonly ReplaySubject<IHybridDbDiagnosticEvent> diagnostics;

        public IObservable<IHybridDbDiagnosticEvent> Diagnostics => diagnostics;

        public HybridDbMessageQueue(IDocumentStore store, Func<IDocumentSession, HybridDbMessage, Task> handler, MessageQueueOptions options = null)
        {
            options ??= new MessageQueueOptions();

            cts = new CancellationTokenSource();
            diagnostics = new ReplaySubject<IHybridDbDiagnosticEvent>(options.DiagnosticsReplayWindow);

            var logger = store.Configuration.Logger;
            var table = store.Configuration.Tables.Values.OfType<QueueTable>().Single();

            Task.Factory.StartNew(async () =>
            {
                logger.LogInformation("Queue started.");

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        using var tx = store.BeginTransaction();
                        using var session = store.OpenSession(tx);

                        // querying on the queue is done in same transaction as the subsequent write, and the message is temporarily removed
                        // from the queue while handling it, so other machines/workers won't try to handle it too.
                        var message = tx.Execute(new DequeueCommand(table));

                        if (message == null)
                        {
                            tx.Complete();
                            
                            diagnostics.OnNext(new QueueIdle());

                            await Task.Delay(options.IdleDelay, cts.Token).ConfigureAwait(false);
                            continue;
                        }

                        try
                        {
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
                                tx.Dispose();

                                logger.LogWarning(exception, "Dispatch of command {commandId} failed. Will retry.", message.Id);
                                
                                continue;
                            }

                            logger.LogError(exception, "Dispatch of command {commandId} failed 5 times. Marks command as failed. Will not retry.", message.Id);

                            tx.Execute(new EnqueueCommand(table, message, "errors"));

                            diagnostics.OnNext(new PoisonMessage(message, exception));

                            retries.TryRemove(message.Id, out _);
                        }

                        tx.Complete();
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception exception)
                    {
                        diagnostics.OnNext(new QueueFailed(exception));

                        logger.LogError(exception, $"{nameof(HybridDbMessageQueue)} failed. Will retry.");
                    }
                }

                logger.LogInformation("Queue stopped.");

            }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Dispose() => cts.Cancel();
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