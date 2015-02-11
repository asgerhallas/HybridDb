using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Studio.Infrastructure
{
    public class EventAggregator : IEventAggregator
    {
        private readonly List<WeakReference> subscribers;

        public EventAggregator()
        {
            subscribers = new List<WeakReference>();
        }

        public void Subscribe(object subscriber)
        {
            if (subscribers.Contains(subscriber))
                return;

            subscribers.Add(new WeakReference(subscriber));
        }

        public void Publish<TMessage>(TMessage message)
        {
            var handlers = subscribers.Where(x => x.IsAlive).Select(x => x.Target).OfType<IHandle<TMessage>>();
            
            foreach (var handler in handlers)
            {
                handler.Handle(message);
            }

            // We clean up each time we publish. Removing all dead references
            subscribers.RemoveAll(x => !x.IsAlive);
        }
    }
}