using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using Newtonsoft.Json.Linq;
using ShinySwitch;

namespace HybridDb.Queue
{
    public static class QueueEx
    {
        const string DefaultMessageOrderKey = "default-message-order";

        public static void UseMessageQueue(this Configuration config, MessageQueueOptions options = null)
        {
            options ??= new MessageQueueOptions();

            if (!config.Register(_ => options, false))
            {
                throw new HybridDbException("Only one message queue can be enabled per store.");
            }

            config.GetOrAddTable(new QueueTable(options.TableName));

            config.Decorate<DmlCommandExecutor>((_, decoratee) => (tx, command) =>
                Switch<object>.On(command)
                    .Match<EnqueueCommand>(enqueueCommand => EnqueueCommand.Execute(config.Serializer.Serialize,
                        tx,
                        enqueueCommand))
                    .Match<DequeueCommand>(dequeueCommand => DequeueCommand.Execute(config.Serializer.Deserialize,
                        tx,
                        dequeueCommand))
                    .Else(() => decoratee(tx,
                        command)));
        }

        public static HybridDbMessage Enqueue(
            this IDocumentSession session,
            object message,
            string topic = null,
            int? order = null,
            Dictionary<string, string> metadata = null
        )
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var resultingOrder = GetMessageOrder(session,
                order);

            return Enqueue(session,
                new HybridDbMessage(Guid.NewGuid().ToString(),
                    message,
                    topic,
                    resultingOrder,
                    metadata), null);
        }

        public static HybridDbMessage Enqueue(
            this IDocumentSession session,
            string id,
            object message,
            string topic = null,
            int? order = null,
            Dictionary<string, string> metadata = null
        )
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var resultingOrder = GetMessageOrder(session,
                order);

            return Enqueue(session,
                new HybridDbMessage(id,
                    message,
                    topic,
                    resultingOrder,
                    metadata), null);
        }

        public static HybridDbMessage Enqueue<T>(
            this IDocumentSession session,
            Func<T, Guid, string> idGenerator,
            T message,
            string topic = null,
            int? order = null,
            Dictionary<string, string> metadata = null
        )
        {
            if (idGenerator == null) throw new ArgumentNullException(nameof(idGenerator));
            if (message == null) throw new ArgumentNullException(nameof(message));

            string IdGenerator(object p, Guid etag) => idGenerator((T)p,
                etag);

            var resultingOrder = GetMessageOrder(session,
                order);

            var envelope = new HybridDbMessage(Guid.NewGuid().ToString(),
                message,
                topic,
                resultingOrder,
                metadata);

            return Enqueue(session,
                envelope,
                IdGenerator);
        }

        public static HybridDbMessage Enqueue(
            this IDocumentSession session,
            HybridDbMessage message,
            Func<object, Guid, string> idGenerator = null
        )
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            if (message.Payload is HybridDbMessage)
            {
                throw new ArgumentException("Enqueued message must not be of type HybridDbMessage.");
            }

            SetCorrelationIds(session, message);

            var queueTable = session.GetQueueTable();

            session.Advanced.Defer(new EnqueueCommand(queueTable,
                message,
                idGenerator));

            return message;
        }

        public static void SetDefaultMessageOrder(this IDocumentSession session, int order) =>
            session.Advanced.SessionData[DefaultMessageOrderKey] = order;

        public static void ClearDefaultMessageOrder(this IDocumentSession session) =>
            session.Advanced.SessionData.Remove(DefaultMessageOrderKey);

        public static int GetDefaultMessageOrder(this IDocumentSession session) =>
            TryGetDefaultMessageOrder(session) ?? 0;

        static int? TryGetDefaultMessageOrder(IDocumentSession session) =>
            session.Advanced.SessionData.TryGetValue(DefaultMessageOrderKey,
                out var defaultOrder)
                ? (int)defaultOrder
                : null;

        static int GetMessageOrder(IDocumentSession session, int? order) =>
            order ?? TryGetDefaultMessageOrder(session) ?? 0;

        static void SetCorrelationIds(IDocumentSession session, HybridDbMessage newMessage)
        {
            if (session.Advanced.SessionData.TryGetValue(MessageContext.Key, out var value) &&
                value is MessageContext messageContext &&
                messageContext.IncomingMessage.Metadata
                    .TryGetValue(HybridDbMessage.CorrelationIdsKey, out var correlationIds))
            {
                // Override correlation ID for derived messages.
                newMessage.OverrideCorrelationId(messageContext.IncomingMessage.CorrelationId);

                var newCorrelationIds = JArray.Parse(correlationIds);

                newCorrelationIds.Add(newMessage.Id);

                newMessage.Metadata.Add(HybridDbMessage.CorrelationIdsKey,
                    newCorrelationIds.ToString());

                return;
            }

            newMessage.Metadata.Add(HybridDbMessage.CorrelationIdsKey,
                new JArray(newMessage.Id).ToString());
        }

        static QueueTable GetQueueTable(this IDocumentSession session) =>
            session.Advanced.DocumentStore.Configuration.Tables.Values.OfType<QueueTable>().SingleOrDefault()
            ?? throw new HybridDbException("Queue is not enabled. Run configuration.UseMessageQueue() when setting up HybridDb.");
    }
}