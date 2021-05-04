using System;
using System.Reactive.Linq;
using HybridDb.Queue;

namespace HybridDb.Tests.Queue
{
    public class TestMessageQueueOptions : MessageQueueOptions
    {
        public TestMessageQueueOptions()
        {
            ObserveQueue = o =>
            {
                var replay = o.Replay(TimeSpan.FromSeconds(60));

                replay.Connect();

                return replay;
            };
        }
    }
}