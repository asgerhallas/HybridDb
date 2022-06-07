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

            config.Decorate<Func<DocumentTransaction, DmlCommand, Func<object>>>((_, decoratee) => (tx, command) => () =>
                Switch<object>.On(command)
                    .Match<EnqueueCommand>(enqueueCommand => EnqueueCommand.Execute(config.Serializer.Serialize, tx, enqueueCommand))
                    .Match<DequeueCommand>(dequeueCommand => DequeueCommand.Execute(config.Serializer.Deserialize, tx, dequeueCommand))
                    .Else(() => decoratee(tx, command)()));
        }

        public static void Enqueue(this IDocumentSession session, object message, string topic = null) => 
            Enqueue(session, Guid.NewGuid().ToString(), message, topic);

        public static void Enqueue(this IDocumentSession session, string id, object message, string topic = null)
        {
            var queueTable = session.Advanced.DocumentStore.Configuration.Tables.Values.OfType<QueueTable>().Single();

            session.Advanced.Defer(new EnqueueCommand(queueTable, new HybridDbMessage(id, message, topic)));
        }

        public static void Enqueue<T>(this IDocumentSession session, Func<T, Guid, string> idGenerator, T message, string topic = null)
        {
            var queueTable = session.Advanced.DocumentStore.Configuration.Tables.Values.OfType<QueueTable>().Single();

            string IdGenerator(object p, Guid etag) => idGenerator((T)p, etag);

            session.Advanced.Defer(new EnqueueCommand(queueTable, new HybridDbMessage(Guid.NewGuid().ToString(), message, topic), IdGenerator));
        }
    }
}