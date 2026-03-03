using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace HybridDb.Queue
{
    public static class MessageHandlerDispatcher
    {
        static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo HandleMethod)> cache = new();

        public static Func<IDocumentSession, HybridDbMessage, Task> For(
            Func<Type, IEnumerable<object>> resolveHandlers)
        {
            if (resolveHandlers == null) throw new ArgumentNullException(nameof(resolveHandlers));
            return async (session, message) =>
            {
                var (handlerType, handleMethod) = cache.GetOrAdd(message.Payload.GetType(), t =>
                {
                    var ht = typeof(IMessageHandler<>).MakeGenericType(t);
                    return (ht, ht.GetMethod(nameof(IMessageHandler<object>.Handle)));
                });
                foreach (var handler in resolveHandlers(handlerType))
                {
                    await (Task)handleMethod.Invoke(handler, new[] { session, message.Payload });
                }
            };
        }
    }
}
