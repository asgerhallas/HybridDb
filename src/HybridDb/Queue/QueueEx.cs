using System;
using System.Linq;
using HybridDb.Config;
using ShinySwitch;

namespace HybridDb.Queue
{
    public static class QueueEx
    {
        public static void UseMessageQueue(this Configuration config, MessageQueueOptions options = null)
        {
            options ??= new MessageQueueOptions();

            if (!config.Register(_ => options, overwriteExisting: false))
                throw new HybridDbException("Only one message queue can be enabled per store.");

            config.GetOrAddTable(new QueueTable(options.TableName));

            config.Decorate<DmlCommandExecutor>((_, decoratee) => (tx, command) => 
                Switch<object>.On(command)
                    .Match<EnqueueCommand>(enqueueCommand => EnqueueCommand.Execute(config.Serializer.Serialize, tx, enqueueCommand))
                    .Match<DequeueCommand>(dequeueCommand => DequeueCommand.Execute(config.Serializer.Deserialize, tx, dequeueCommand))
                    .Else(() => decoratee(tx, command)));
        }

        public static void Enqueue(this IDocumentSession session, object message, string topic = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            Enqueue(session, new HybridDbMessage(Guid.NewGuid().ToString(), message, topic));
        }

        public static void Enqueue(this IDocumentSession session, string id, object message, string topic = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            Enqueue(session, new HybridDbMessage(id, message, topic));
        }

        public static void Enqueue<T>(this IDocumentSession session, Func<T, Guid, string> idGenerator, T message, string topic = null)
        {
            if (idGenerator == null) throw new ArgumentNullException(nameof(idGenerator));
            if (message == null) throw new ArgumentNullException(nameof(message));

            string IdGenerator(object p, Guid etag) => idGenerator((T)p, etag);

            Enqueue(session, new HybridDbMessage(Guid.NewGuid().ToString(), message, topic), IdGenerator);
        }

        public static void Enqueue(this IDocumentSession session, HybridDbMessage message, Func<object, Guid, string> idGenerator = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            if (message.Payload is HybridDbMessage)
            {
                throw new ArgumentException("Enqueued message must not be of type HybridDbMessage.");
            }

            var queueTable = session.Advanced.DocumentStore.Configuration.Tables.Values.OfType<QueueTable>().Single();

            session.Advanced.Defer(new EnqueueCommand(queueTable, message, idGenerator));
        }
    }
}