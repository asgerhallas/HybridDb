using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Indentional;

namespace HybridDb.Queue
{
    public class BlockingTestObserver : IObserver<IHybridDbQueueEvent>
    {
        readonly TimeSpan timeout;
        readonly BlockingCollection<IHybridDbQueueEvent> queue = new();
        readonly List<IHybridDbQueueEvent> history = new();
        readonly SemaphoreSlim gate = new(0);
        readonly CancellationTokenSource cts = new();

        volatile bool waitingAtTheGate = true;

        public BlockingTestObserver(TimeSpan timeout)
        {
            this.timeout = timeout;
        }

        public IReadOnlyList<IHybridDbQueueEvent> History => history;

        public IDisposable Subscribe(IObservable<IHybridDbQueueEvent> observable) => observable.Subscribe(this);

        public void OnCompleted()
        {
                
        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(IHybridDbQueueEvent value)
        {
            if (value.CancellationToken.IsCancellationRequested)
            {
                StopBlocking();
            }

            if (cts.IsCancellationRequested) return;

            queue.Add(value);
            history.Add(value);

            waitingAtTheGate = true;

            var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(value.CancellationToken, cts.Token);

            gate.Wait(linkedCts.Token);
        }

        public Task AdvanceBy1()
        {
            waitingAtTheGate = false;

            gate.Release();

            return Task.CompletedTask;
        }

        public record GetNextResult
        {
            GetNextResult() { }

            public record Event(IHybridDbQueueEvent Value) : GetNextResult;
            public record Timeout : GetNextResult;
            public record Locked : GetNextResult;
        }

        /// <summary>
        /// Wait for the next event for the given timeout and return it.
        /// </summary>
        /// <returns>An event if any is present within the timeout, or else null indicating a timeout.</returns>
        public Task<GetNextResult> GetNext() =>
            CatchAndCancel(async () =>
            {
                cts.Token.ThrowIfCancellationRequested();

                if (waitingAtTheGate)
                {
                    return queue.TryTake(out var next)
                        ? (GetNextResult)new GetNextResult.Event(next)
                        : new GetNextResult.Locked();
                }
                else
                {
                    return queue.TryTake(out var next, timeout)
                        ? new GetNextResult.Event(next)
                        : new GetNextResult.Timeout();
                }
            });

        public Task<IHybridDbQueueEvent> GetNextOrNull() =>
            CatchAndCancel(async () =>
                await GetNext() switch
                {
                    GetNextResult.Event @event => @event.Value,
                    GetNextResult.Timeout => null,
                    GetNextResult.Locked => throw new InvalidOperationException(@"
                        No values have been observed since last check, and the observable 
                        is not currently advancing. This will result in a hang. 
                        Have you forgot to call AdvanceBy1/AdvanceUntil/AdvanceToEnd?.
                    ".Indent() + GetHistoryString()),
                    _ => throw new ArgumentOutOfRangeException()
                });

        public Task<IHybridDbQueueEvent> GetNextOrNullAdvanceIfNeccessary() =>
            CatchAndCancel(async () =>
            {
                var next = await GetNext();

                if (next is GetNextResult.Locked)
                {
                    await AdvanceBy1();
                    return await GetNextOrNull();
                }

                return next switch
                {
                    GetNextResult.Event @event => @event.Value,
                    GetNextResult.Timeout => null,
                    _ => throw new ArgumentOutOfRangeException()
                };
            });


        public Task<T> NextShouldBe<T>() where T : class =>
            CatchAndCancel(async () =>
                await GetNextOrNull() switch
                {
                    null => throw new TimeoutException($"Timeout waiting for {typeof(T)}." + GetHistoryString()),
                    T t => t,
                    var next => throw new Exception($"Expected {typeof(T)}, got {next.GetType()}. {GetHistoryString()}")
                }
            );

        public async Task NextShouldBeThenAdvanceBy1<T>() where T : class
        {
            await NextShouldBe<T>();
            await AdvanceBy1();
        }

        public async Task<T> AdvanceBy1ThenNextShouldBe<T>() where T : class
        {
            await AdvanceBy1();
            return await NextShouldBe<T>();
        }

        public Task<T> AdvanceUntil<T>() =>
            CatchAndCancel(async () =>
            {
                do
                {
                    var next = await GetNextOrNullAdvanceIfNeccessary();

                    if (next == null)
                    {
                        throw new TimeoutException($"Timeout waiting for {typeof(T)}." + GetHistoryString());
                    }

                    if (next is T t)
                    {
                        return t;
                    }
                } while (true);
            });

        public Task WaitForNothingToHappen() =>
            CatchAndCancel(async () =>
            {
                if (await GetNextOrNull() is not { } next) return;
                
                throw new Exception($"Expected nothing (timeout), got {next.GetType()}. {GetHistoryString()}");
            });

        public void StopBlocking() => cts.Cancel();

        string GetHistoryString() =>
            Environment.NewLine + 
            Environment.NewLine +
            "Observed events until now: " +
            Environment.NewLine +
            Environment.NewLine +
            string.Join(Environment.NewLine, history.ToList().Select((x, i) => $"  {i+1}. {x}")) +
            Environment.NewLine;

        async Task CatchAndCancel(Func<Task> func)
        {
            try
            {
                await func().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // The blocking observer must be cancelled before throwing,
                // or else the queue will hang in dispose.
                cts.Cancel();
                throw;
            }
        }

        async Task<T> CatchAndCancel<T>(Func<Task<T>> func)
        {
            try
            {
                return await func().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // The blocking observer must be cancelled before throwing,
                // or else the queue will hang in dispose.
                cts.Cancel();
                throw;
            }
        }
    }
}