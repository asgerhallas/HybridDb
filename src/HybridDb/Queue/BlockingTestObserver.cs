using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Indentional;

namespace HybridDb.Queue
{
    public class BlockingTestObserver : IObserver<object>
    {
        readonly TimeSpan timeout;
        readonly BlockingCollection<object> queue = new();
        readonly List<object> history = new();
        readonly SemaphoreSlim gate = new(0);

        CancellationToken observableCancellationToken = new();
        readonly CancellationTokenSource observerCancellationTokenSource = new();

        volatile int waitingAtTheGate;

        public BlockingTestObserver(TimeSpan timeout) => this.timeout = timeout;

        public IDisposable Subscribe<T>(IObservable<T> observable, CancellationToken cancellationToken)
        {
            observableCancellationToken = CancellationTokenSource
                .CreateLinkedTokenSource(observableCancellationToken, cancellationToken)
                .Token;

            return observable.Select(x => (object)x).Subscribe(this);
        }

        public void OnCompleted()
        {
                
        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(object value)
        {
            if (observableCancellationToken.IsCancellationRequested)
            {
                StopBlocking();
            }

            if (observerCancellationTokenSource.IsCancellationRequested) return;

            waitingAtTheGate = 1;

            // Thread waiting on GetNext will immediately continue, when value is added
            // This will in turn often result in AdvanceBy1 to be executed before
            // the rest the OnNext call is completed. Be aware!
            queue.Add(value, observableCancellationToken); 
            history.Add(value);

            var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(observableCancellationToken, observerCancellationTokenSource.Token);

            gate.Wait(linkedCts.Token);
        }

        public Task AdvanceBy1()
        {
            if (observerCancellationTokenSource.IsCancellationRequested) return Task.CompletedTask;

            waitingAtTheGate = 0;

            gate.Release();

            return Task.CompletedTask;
        }

        public record GetNextResult
        {
            GetNextResult() { }

            public record Event(object Value) : GetNextResult;
            public record Timeout : GetNextResult;
            public record Locked : GetNextResult;
        }

        /// <summary>
        /// Wait for the next event for the given timeout and return it.
        /// </summary>
        /// <returns>An event if any is present within the timeout, or else null indicating a timeout.</returns>
        public async Task<GetNextResult> GetNext()
        {
            observerCancellationTokenSource.Token.ThrowIfCancellationRequested();

            if (waitingAtTheGate == 1)
            {
                return queue.TryTake(out var next)
                    ? new GetNextResult.Event(next)
                    : new GetNextResult.Locked();
            }
            else
            {
                return queue.TryTake(out var next, timeout)
                    ? new GetNextResult.Event(next)
                    : new GetNextResult.Timeout();
            }
        }

        public async Task<object> GetNextOrNull() =>
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
            };

        public async Task<object> GetNextOrNullAdvanceIfNeccessary()
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
        }


        public async Task<T> NextShouldBe<T>() where T : class =>
            await GetNextOrNull() switch
            {
                null => throw new TimeoutException($"Timeout waiting for {typeof(T)}." + GetHistoryString()),
                T t => t,
                var next => throw new Exception($"Expected {typeof(T)}, got {next.GetType()}. {GetHistoryString()}")
            };

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

        public async Task<T> AdvanceUntil<T>()
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

                await AdvanceBy1();

            } while (true) ;
        }

        public async Task WaitForNothingToHappen()
        {
            if (await GetNextOrNull() is not { } next) return;
                
            throw new Exception($"Expected nothing (timeout), got {next.GetType()}. {GetHistoryString()}");
        }

        public void StopBlocking() => observerCancellationTokenSource.Cancel();

        string GetHistoryString() =>
            Environment.NewLine + 
            Environment.NewLine +
            "Observed events until now: " +
            Environment.NewLine +
            Environment.NewLine +
            string.Join(Environment.NewLine, history.ToList().Select((x, i) => $"  {i+1}. {x}")) +
            Environment.NewLine;
    }
}