using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HybridDb.Queue
{
    public static class MessageHandlerDispatcher
    {
        static readonly ConcurrentDictionary<Type, Type> cache = new();

        public static Func<IDocumentSession, HybridDbMessage, Task> For(
            Func<Type, IEnumerable<object>> resolveHandlers) =>
            async (session, message) =>
            {
                var handlerType = cache.GetOrAdd(message.Payload.GetType(),
                    t => typeof(IMessageHandler<>).MakeGenericType(t));
                foreach (var handler in resolveHandlers(handlerType))
                {
                    await ((dynamic)handler).Handle(session, message.Payload);
                }
            };
    }
}
