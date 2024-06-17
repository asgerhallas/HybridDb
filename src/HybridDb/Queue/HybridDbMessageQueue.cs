using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
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
        readonly CancellationTokenSource cts;
        readonly ConcurrentDictionary<string, int> retries = new();
        readonly ConcurrentDictionary<IDocumentSession, int> sessions = new();
        readonly ISubject<IHybridDbQueueEvent> events = Subject.Synchronize(new Subject<IHybridDbQueueEvent>());

        readonly IDocumentStore store;
        readonly ILogger logger;
        readonly QueueTable table;
        readonly MessageQueueOptions options;
        readonly Func<IDocumentSession, HybridDbMessage, Task> handler;
        readonly SemaphoreSlim localEnqueues = new(0);
        readonly IDisposable subscribeDisposable;

        public HybridDbMessageQueue(
            IDocumentStore store,
            Func<IDocumentSession, HybridDbMessage, Task> handler
        )
        {
            this.store = store;
            this.handler = handler;

            if (!store.Configuration.TryResolve(out options))
            {
                throw new HybridDbException("MessageQueue is not enabled. Please run UseMessageQueue in the configuration.");
            }

            Events = events;

            if (options.Replay != null)
            {
                // unsubscribes when events are completed (in Dispose)
                events.Subscribe(options.Replay);

                ReplayedEvents = options.Replay;
            }

            // if options are set up to return a disposable that is 
            // not a subscription to events, then it won't automatically
            // be disposed when events are completed. So we need to 
            // keep the disposable and call on Dispose.
            subscribeDisposable = options.Subscribe(events);

            logger = store.Configuration.Logger;
            table = store.Configuration.Tables.Values.OfType<QueueTable>().Single();

            if (options.UseLocalEnqueueTrigger)
            {
                store.Configuration.AddEventHandler(x =>
                {
                    if (x is not SaveChanges_AfterExecuteCommands saved) return;

                    var enqueueCommands = saved.ExecutedCommands.Keys
                        .OfType<EnqueueCommand>()
                        .Count(command => options.InboxTopics.Contains(command.Message.Topic));

                    if (enqueueCommands <= 0) return;

                    localEnqueues.Release(enqueueCommands);
                });
            }

            cts = options.GetCancellationTokenSource();

            MainLoop = Task.Factory
                .StartNew(async () =>
                    {
                        events.OnNext(new QueueStarting(cts.Token));

                        logger.LogInformation($"""
                            Queue started. 
                            Reading messages with version {options.Version} or older, 
                            from topics '{string.Join("', '", options.InboxTopics)}'.
                            """.Indent());
    
                        using var semaphore = new SemaphoreSlim(options.MaxConcurrency);

                        while (!cts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                var release = await WaitAsync(semaphore);

                                try
                                {
                                    var (session, message) = await NextMessage();

                                    try
                                    {
                                        await Task.Factory.StartNew(async () =>
                                        {
                                            try
                                            {
                                                using var _ = Time("HandleMessage");

                                                await HandleMessage(session, message);
                                            }
                                            finally
                                            {
                                                // open the gate for the next message, when this message is handled.
                                                release();
                                                DisposeSession(session);
                                            }
                                        },
                                        cts.Token,
                                        TaskCreationOptions.DenyChildAttach,
                                        TaskScheduler.Default);
                                    }
                                    catch
                                    {
                                        release();
                                        DisposeSession(session);

                                        throw;
                                    }
                                }
                                catch
                                {
                                    release();

                                    throw;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception exception)
                            {
                                events.OnNext(new QueueFailed(exception, cts.Token));

                                logger.LogError(exception, $"{nameof(HybridDbMessageQueue)} failed. Will retry.");

                                await Task.Delay(options.ExceptionBackoff, cts.Token);
                            }
                        }

                        DisposeAllSessions();

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

        public Task MainLoop { get; }
        public IObservable<IHybridDbQueueEvent> Events { get; }
        public IObservable<IHybridDbQueueEvent> ReplayedEvents { get; } = Observable.Create<IHybridDbQueueEvent>(ThrowOnSubscribe);

        public void Dispose()
        {
            cts.Cancel();

            using (Time("dispose, wait for shutdown"))
            {
                MainLoop.ContinueWith(x => x).Wait();
            }

            DisposeAllSessions();

            events.OnCompleted();
            subscribeDisposable.Dispose();
        }

        static Action ThrowOnSubscribe(IObserver<IHybridDbQueueEvent> _) =>
            throw new InvalidOperationException("You must set MessageQueueOptions.Replay if you want to subscribe to replayed events.");

        IDocumentSession BeginSession()
        {
            cts.Token.ThrowIfCancellationRequested();

            // We create the session first to not rely on external implementations of CreateSession
            // to correctly enlist the transaction. We get the CommitId from the created session and use
            // it to create the tx and then enlist. Then we use the session as a vehicle for the transaction
            // as they should always be in sync.
            var session = options.CreateSession(store);

            var tx = store.BeginTransaction(session.CommitId, connectionTimeout: options.ConnectionTimeout);

            try
            {
                session.Advanced.Enlist(tx);

                if (!sessions.TryAdd(session, 0))
                {
                    throw new InvalidOperationException("Transaction could not be tracked.");
                }

                return session;
            }
            catch (Exception)
            {
                tx.Dispose();

                throw;
            }
        }

        void DisposeSession(IDocumentSession session)
        {
            if (!sessions.TryRemove(session, out _)) return;

            try
            {
                session.Advanced.DocumentTransaction.Dispose();
                session.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dispose transaction failed.");
            }
        }

        void DisposeAllSessions()
        {
            foreach (var tx in sessions)
            {
                DisposeSession(tx.Key);
            }
        }

        async Task<Action> WaitAsync(SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync(cts.Token);

            var released = 0;

            return () =>
            {
                if (Interlocked.Exchange(ref released, 1) == 1) return;

                // Release could be called after the main loop has ended, and the semaprhore has 
                // been disposed, if a handler thread is still working during shutdown. We don't 
                // wait for handlers to run to completion as it would risk the queue to hang during shutdown.
                if (cts.IsCancellationRequested) return;

                semaphore.Release();
            };
        }

        async Task<(IDocumentSession, HybridDbMessage)> NextMessage()
        {
            var session = BeginSession();

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    events.OnNext(new QueuePolling(cts.Token));

                    var count = localEnqueues.CurrentCount;

                    // Querying on the queue is done in same transaction as the subsequent write, and the message is temporarily removed
                    // from the queue while handling it, so other machines/workers won't try to handle it too.
                    var message = session.Advanced.DocumentTransaction.Execute(new DequeueCommand(table, options.InboxTopics));

                    if (message != null) return (session, message);

                    DisposeSession(session);

                    events.OnNext(new QueueEmpty(cts.Token));

                    //// We only get here if the queue is empty, and that means that we must have handled all local enqueues
                    //// that was counted _before_ the dequeue. If any has been locally enqueued after the last empty dequeue,
                    //// we keep those to go another round immediately.
                    for (; count > 0; count--)
                    {
                        // ReSharper disable once MethodHasAsyncOverload
                        // We know that we have `count` released locks, so I believe sync Wait is faster here
                        // though it has to be measured.
                        localEnqueues.Wait(cts.Token);
                    }

                    // Wait for any local enqueue or for the timeout (IdleDelay) to check for remote enqueues at an interval.
                    await localEnqueues.WaitAsync(options.IdleDelay, cts.Token).ConfigureAwait(false);

                    session = BeginSession();
                }   

                return (session, await Task.FromCanceled<HybridDbMessage>(cts.Token).ConfigureAwait(false));
            }
            catch
            {
                DisposeSession(session);

                throw;
            }
        }

        async Task HandleMessage(IDocumentSession session, HybridDbMessage message)
        {
            var tx = session.Advanced.DocumentTransaction;

            var context = new MessageContext(message);

            try
            {
                events.OnNext(new MessageReceived(context, message, cts.Token));
                
                tx.SqlTransaction.Save("MessageReceived");

                session.Advanced.SessionData.Add(MessageContext.Key, context);

                events.OnNext(new MessageHandling(session, context, message, cts.Token));

                await handler(session, message);

                events.OnNext(new MessageHandled(session, context, message, cts.Token));

                session.SaveChanges();
                
                tx.Complete();

                events.OnNext(new MessageCommitted(session, context, message, cts.Token));
            }
            catch (Exception exception)
            {
                if (cts.IsCancellationRequested) return;

                var failures = retries.AddOrUpdate(message.Id, _ => 1, (_, current) => current + 1);

                // TODO: log here to ensure we get a log before a new exception is raised

                events.OnNext(new MessageFailed(context, message, exception, failures, cts.Token));

                if (failures < 5)
                {
                    logger.LogWarning(exception, "Dispatch of command {commandId} failed. Will retry.", message.Id);

                    return;
                }

                tx.SqlTransaction.Rollback("MessageReceived");

                logger.LogError(exception, "Dispatch of command {commandId} failed 5 times. Marks command as failed. Will not retry.", message.Id);

                tx.Execute(new EnqueueCommand(table, message with { Topic = $"errors/{message.Topic}" }));

                tx.Complete();

                events.OnNext(new PoisonMessage(context, message, exception, cts.Token));

                retries.TryRemove(message.Id, out _);
            }
        }

        public IDisposable Time(string text)
        {
            var startNew = Stopwatch.StartNew();

            return Disposable.Create(() => store.Configuration
                .Logger.LogDebug($"HybridDbMessageQueue: Timed {text}: {startNew.ElapsedMilliseconds}ms."));
        }
    }
}