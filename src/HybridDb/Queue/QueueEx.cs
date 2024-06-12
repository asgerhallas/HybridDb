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
                    .Match<EnqueueCommand>(enqueueCommand => EnqueueCommand.Execute(config.Serializer.Serialize, tx, enqueueCommand))
                    .Match<DequeueCommand>(dequeueCommand => DequeueCommand.Execute(config.Serializer.Deserialize, tx, dequeueCommand))
                    .Else(() => decoratee(tx, command)));
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

            var resultingOrder = GetMessageOrder(session, order);

            var id = Guid.NewGuid().ToString();

            var correlationId = GetIncomingMessageCorrelationIdOrNull(session);

            return Enqueue(
                session,
                new HybridDbMessage(
                    id,
                    message,
                    topic,
                    resultingOrder,
                    correlationId,
                    metadata),
                null);
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

            var resultingOrder = GetMessageOrder(session, order);

            var correlationId = GetIncomingMessageCorrelationIdOrNull(session);

            return Enqueue(
                session,
                new HybridDbMessage(
                    id,
                    message,
                    topic,
                    resultingOrder,
                    correlationId,
                    metadata),
                null);
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

            string IdGenerator(object p, Guid etag) => idGenerator((T)p, etag);

            var resultingOrder = GetMessageOrder(session, order);

            var correlationId = GetIncomingMessageCorrelationIdOrNull(session);

            var envelope = new HybridDbMessage(
                Guid.NewGuid().ToString(),
                message,
                topic,
                resultingOrder,
                correlationId,
                metadata);

            return Enqueue(session, envelope, IdGenerator);
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

            AddBreadcrumb(session, message);

            var queueTable = session.GetQueueTable();

            session.Advanced.Defer(new EnqueueCommand(queueTable, message, idGenerator));

            return message;
        }

        public static void SetDefaultMessageOrder(this IDocumentSession session, int order) =>
            session.Advanced.SessionData[DefaultMessageOrderKey] = order;

        public static void ClearDefaultMessageOrder(this IDocumentSession session) =>
            session.Advanced.SessionData.Remove(DefaultMessageOrderKey);

        public static int GetDefaultMessageOrder(this IDocumentSession session) =>
            TryGetDefaultMessageOrder(session) ?? 0;

        static int? TryGetDefaultMessageOrder(IDocumentSession session) =>
            session.Advanced.SessionData.TryGetValue(DefaultMessageOrderKey, out var defaultOrder) ? (int)defaultOrder : null;

        static int GetMessageOrder(IDocumentSession session, int? order) =>
            order ?? TryGetDefaultMessageOrder(session) ?? 0;

        static string GetIncomingMessageCorrelationIdOrNull(IDocumentSession session)
        {
            if (session.Advanced.SessionData.TryGetValue(MessageContext.Key, out var value) &&
                value is MessageContext messageContext)
            {
                return messageContext.IncomingMessage.CorrelationId;
            }

            return null;
        }

        static void AddBreadcrumb(IDocumentSession session, HybridDbMessage newMessage)
        {
            if (session.Advanced.SessionData.TryGetValue(MessageContext.Key, out var value) &&
                value is MessageContext messageContext &&
                messageContext.IncomingMessage.Metadata.TryGetValue(HybridDbMessage.Breadcrumbs, out var breadcrumbs))
            {
                var newBreadcrumbs = JArray.Parse(breadcrumbs);

                // TODO: Bug. This is the provided id, but this can be changed by the IdGenerator later,
                // so it might not actually be correct. See test CorrelationIds_WithIdGenerator.
                newBreadcrumbs.Add(newMessage.Id);

                newMessage.Metadata.Add(HybridDbMessage.Breadcrumbs, newBreadcrumbs.ToString());

                return;
            }

            newMessage.Metadata.Add(HybridDbMessage.Breadcrumbs, new JArray(newMessage.Id).ToString());
        }

        static QueueTable GetQueueTable(this IDocumentSession session) =>
            session.Advanced.DocumentStore.Configuration.Tables.Values.OfType<QueueTable>().SingleOrDefault()
            ?? throw new HybridDbException("Queue is not enabled. Run configuration.UseMessageQueue() when setting up HybridDb.");
    }
}