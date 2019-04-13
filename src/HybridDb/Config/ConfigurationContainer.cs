using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Config
{
    public class ConfigurationContainer : IContainerActivator, IDisposable
    {
        readonly List<object> tracked = new List<object>();
        readonly ConcurrentDictionary<Type, Lazy<object>> factories = new ConcurrentDictionary<Type, Lazy<object>>();

        public void Register<T>(Func<IContainerActivator, T> factory)
        {
            var activator = new Lazy<object>(() => Track(factory(this)), isThreadSafe: true);

            factories.AddOrUpdate(typeof(T), key => activator, (key, current) => activator);
        }

        public void Decorate<T>(Func<IContainerActivator, T, T> factory)
        {
            factories.AddOrUpdate(typeof(T),
                key => throw new InvalidOperationException($"There's no {key} to decorate in the container."),
                (key, decoratee) => new Lazy<object>(() => Track(factory(this, (T) decoratee.Value)), isThreadSafe: true));
        }

        public T Resolve<T>()
        {
            if (!factories.TryGetValue(typeof(T), out var activator))
            {
                throw new InvalidOperationException($"No activator registered for {typeof(T)}");
            }

            return (T) activator.Value;
        }

        public void Dispose()
        {
            foreach (var disposable in tracked.OfType<IDisposable>())
            {
                disposable.Dispose();
            }
        }

        T Track<T>(T obj)
        {
            tracked.Add(obj);
            return obj;
        }
    }
}