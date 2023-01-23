using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HybridDb.Queue
{
    public class DedicatedThreadScheduler : TaskScheduler, IDisposable
    {
        readonly BlockingCollection<Task> tasks = new();
        readonly Thread thread;
        readonly CancellationTokenSource cts;
        
        volatile bool disposed;

        public DedicatedThreadScheduler()
        {
            cts = new CancellationTokenSource();
            
            thread = new Thread(Run);
            thread.Start();
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

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => 
            Thread.CurrentThread == thread && TryExecuteTask(task);
    }
}