using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace HybridDb.Queue
{
    public static class MessageHandlerDispatcher
    {
        static readonly ConcurrentDictionary<Type, MethodInfo> cache = new();

        public static Func<IDocumentSession, HybridDbMessage, Task> For(Func<Type, IEnumerable<object>> resolveHandlers)
        {
            if (resolveHandlers == null)
            {
                throw new ArgumentNullException(nameof(resolveHandlers));
            }

            return async (session, message) =>
            {
                var handleMethod = cache.GetOrAdd(
                    message.Payload.GetType(),
                    t => typeof(IMessageHandler<>)
                        .MakeGenericType(t)
                        .GetMethod(nameof(IMessageHandler<object>.Handle)));

                foreach (var handler in resolveHandlers(handleMethod.ReflectedType))
                {
                    try
                    {
                        await ((Task)handleMethod.Invoke(handler, new[] { session, message.Payload }))!;
                    }
                    catch (TargetInvocationException e)
                    {
                        ExceptionDispatchInfo.Throw(e.InnerException ?? e);
                    }
                }
            };
        }
    }
}
