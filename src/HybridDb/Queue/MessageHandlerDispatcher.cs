using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace HybridDb.Queue
{
    public static class MessageHandlerDispatcher
    {
        static readonly ConcurrentDictionary<Type, (Type handlerType, MethodInfo method)> cache = new();

        public static Func<IDocumentSession, HybridDbMessage, Task> For(
            Func<Type, IEnumerable<object>> resolveHandlers) =>
            async (session, message) =>
            {
                var payloadType = message.Payload.GetType();
                var (handlerType, method) = cache.GetOrAdd(payloadType, t =>
                {
                    var ht = typeof(IMessageHandler<>).MakeGenericType(t);
                    var m = ht.GetMethod(nameof(IMessageHandler<object>.Handle))!;
                    return (ht, m);
                });
                foreach (var handler in resolveHandlers(handlerType))
                {
                    await ((Task)method.Invoke(handler, new object[] { session, message.Payload }) ?? Task.CompletedTask);
                }
            };
    }
}
