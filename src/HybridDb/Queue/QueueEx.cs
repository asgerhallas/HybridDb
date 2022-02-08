using System;
using System.Linq;
using HybridDb.Config;
using ShinySwitch;

namespace HybridDb.Queue
{
    public static class QueueEx
    {
        public static void UseMessageQueue(this Configuration config, MessageQueueOptions options, string tablename = "messages")
        {
            if (!config.Register(_ => options))
                throw new HybridDbException("Only one message queue can be enabled per store.");

            config.GetOrAddTable(new QueueTable(tablename));

            config.Decorate<Func<DocumentTransaction, DmlCommand, Func<object>>>((_, decoratee) => (tx, command) => () =>
                Switch<object>.On(command)
                    .Match<EnqueueCommand>(enqueueCommand => EnqueueCommand.Execute(config.Serializer.Serialize, tx, enqueueCommand))
                    .Match<DequeueCommand>(dequeueCommand => DequeueCommand.Execute(config.Serializer.Deserialize, tx, dequeueCommand))
                    .Else(() => decoratee(tx, command)()));
        }

        public static void Enqueue(this IDocumentSession session, HybridDbMessage message, string topic = null)
        {
            var queueTable = session.Advanced.DocumentStore.Configuration.Tables.Values.OfType<QueueTable>().Single();

            session.Advanced.Defer(new EnqueueCommand(queueTable, message, topic));
        }
    }
}