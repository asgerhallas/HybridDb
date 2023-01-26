using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HybridDb.Queue
{
    public class DedicatedThreadScheduler : TaskScheduler, IDisposable
    {
        readonly BlockingCollection<Task> tasks = new();
        readonly List<Thread> threads;
        readonly CancellationTokenSource cts;
        
        volatile bool disposed;

        public DedicatedThreadScheduler(int numberOfThreads)
        {
            cts = new CancellationTokenSource();

            threads = Enumerable.Range(0, numberOfThreads).Select(x =>
            {
                var thread = new Thread(Run);
                thread.Start();

                return thread;
            }).ToList();
        }

        public void Dispose()
        {
            disposed = true;
            
            cts.Cancel();
        }

        void Run()
        {
            while (!disposed)
            {
                try
                {
                    TryExecuteTask(tasks.Take(cts.Token));
                }
                catch (OperationCanceledException)
                { 
                    return;
                }
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks() => tasks;

        protected override void QueueTask(Task task) => tasks.Add(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (taskWasPreviouslyQueued) return false;

            return threads.Contains(Thread.CurrentThread) && TryExecuteTask(task);
        }
    }
}