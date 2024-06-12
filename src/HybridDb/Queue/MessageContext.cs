using System.Collections.Generic;

namespace HybridDb.Queue
{
    public class MessageContext : Dictionary<string, object>
    {
        public const string Key = nameof(MessageContext);

        public MessageContext(HybridDbMessage incomingMessage) => IncomingMessage = incomingMessage;

        public HybridDbMessage IncomingMessage { get; }
    }
}