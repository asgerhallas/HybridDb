using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using HybridDb.Config;
using Newtonsoft.Json.Linq;
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

        public static HybridDbMessage Enqueue(this IDocumentSession session, object message, string topic = null, Dictionary<string, string> metadata = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            return Enqueue(session, new HybridDbMessage(Guid.NewGuid().ToString(), message, topic, metadata));
        }

        public static HybridDbMessage Enqueue(this IDocumentSession session, string id, object message, string topic = null, Dictionary<string, string> metadata = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            return Enqueue(session, new HybridDbMessage(id, message, topic, metadata));
        }

        public static HybridDbMessage Enqueue<T>(this IDocumentSession session, Func<T, Guid, string> idGenerator, T message, string topic = null, Dictionary<string, string> metadata = null)
        {
            if (idGenerator == null) throw new ArgumentNullException(nameof(idGenerator));
            if (message == null) throw new ArgumentNullException(nameof(message));

            string IdGenerator(object p, Guid etag) => idGenerator((T)p, etag);

            var envelope = new HybridDbMessage(Guid.NewGuid().ToString(), message, topic, metadata);

            return Enqueue(session, envelope, IdGenerator);
        }

        public static HybridDbMessage Enqueue(this IDocumentSession session, HybridDbMessage message, Func<object, Guid, string> idGenerator = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            if (message.Payload is HybridDbMessage)
            {
                throw new ArgumentException("Enqueued message must not be of type HybridDbMessage.");
            }

            message.Metadata.Add(HybridDbMessage.CorrelationIdsKey, GetNextCorrelationIds(session, message));

            var queueTable = session.Advanced.DocumentStore.Configuration.Tables.Values.OfType<QueueTable>().Single();

            session.Advanced.Defer(new EnqueueCommand(queueTable, message, idGenerator));

            return message;
        }

        static string GetNextCorrelationIds(IDocumentSession session, HybridDbMessage message)
        {
            if (session.Advanced.SessionData.TryGetValue(MessageContext.Key, out var value) &&
                value is MessageContext messageContext && 
                messageContext.IncomingMessage.Metadata.TryGetValue(HybridDbMessage.CorrelationIdsKey, out var correlationIds))
            {
                var nextCorrelationIds = JArray.Parse(correlationIds);
                nextCorrelationIds.Add(message.Id);

                return nextCorrelationIds.ToString();
            }

            return new JArray(message.Id).ToString();
        }
    }
}