using System;
using System.Collections.Concurrent;
using System.Linq;
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
        // [ ] Enqueuing should be idempotent. It should ignore exceptions from primary key violations and just not insert the message.
        // [x] Querying on the queue should be done in same transaction as the subsequent write. And the message should be temporarily removed
        //    from the queue while handling it, so other machines/workers won't try to handle it too.
        // [ ] Handling of messages could be idempotent too, by soft deleting them when done, but still keeping it around to guard against
        //    subsequent redelivery with the same id.
        // [x] Testing should be made reliable with a newer HybridDb that support multiple concurrent readers on the same test-session.

        readonly CancellationTokenSource cts;
        readonly ConcurrentDictionary<string, int> retries = new();
        readonly Subject<(IDocumentSession Session, HybridDbMessage Message)> handling = new();
        readonly Subject<HybridDbMessage> handled = new();

        public IObservable<(IDocumentSession Session, HybridDbMessage Message)> Handling => handling;
        public IObservable<HybridDbMessage> Handled => handled;

        public HybridDbMessageQueue(IDocumentStore store, Func<IDocumentSession, HybridDbMessage, Task> handler, ILogger logger)
        {
            var table = store.Configuration.Tables.Values.OfType<QueueTable>().Single();

            cts = new CancellationTokenSource();

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
                            await Task.Delay(100, cts.Token);
                            continue;
                        }

                        try
                        {
                            handling.OnNext((session, message));

                            await handler(session, message);

                            session.SaveChanges();

                            handled.OnNext(message);
                        }
                        catch (Exception e)
                        {
                            var failures = retries.AddOrUpdate(message.Id, key => 1, (key, current) => current + 1);

                            if (failures >= 5)
                            {
                                logger.LogError(e, "Dispatch of command {commandId} failed 5 times. Marks command as failed. Will not retry.", message.Id);

                                var errorSession = store.OpenSession();
                                errorSession.Load<HybridDbMessage>(message.Id)?.MarkAsFailed();
                                errorSession.SaveChanges();

                                retries.TryRemove(message.Id, out _);
                            }
                            else
                            {
                                logger.LogWarning(e, "Dispatch of command {commandId} failed. Will retry.", message.Id);
                            }

                            continue;
                        }

                        tx.Complete();
                    }
                    catch (TaskCanceledException) { continue; }
                    catch (Exception e)
                    {
                        logger.LogError(e, $"{nameof(HybridDbMessageQueue)} failed. Will retry.");
                    }
                     
                    await Task.Delay(10, cts.Token).ConfigureAwait(false);
                }

                logger.LogInformation("Queue stopped.");

            }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Dispose() => cts.Cancel();
    }

    public abstract record HybridDbMessage(string Id)
    {
        public bool IsFailed { get; private set; }

        public void MarkAsFailed() => IsFailed = true;
    }
}