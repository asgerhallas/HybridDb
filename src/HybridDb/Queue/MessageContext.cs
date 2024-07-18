using System.Collections.Generic;

namespace HybridDb.Queue
{
    public class SessionContext
    {
        public const string Key = nameof(SessionContext);

        public Dictionary<string, object> Data { get; protected set; } = new();
    }

    public class MessageContext : SessionContext
    {
        public new const string Key = nameof(MessageContext);

        public MessageContext(SessionContext context, HybridDbMessage incomingMessage)
        {
            Data = context.Data;
            IncomingMessage = incomingMessage;
        }

        public HybridDbMessage IncomingMessage { get; }
    }
}