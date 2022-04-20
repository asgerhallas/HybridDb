using System;

namespace HybridDb.Queue
{
    public interface IHybridDbQueueEvent { }

    public record MessageHandling(IDocumentSession Session, MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageHandled(IDocumentSession Session, MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageFailed(MessageContext Context, HybridDbMessage Message, Exception Exception) : IHybridDbQueueEvent;
    public record PoisonMessage(MessageContext Context, HybridDbMessage Message, Exception Exception) : IHybridDbQueueEvent;
    public record QueueStarting : IHybridDbQueueEvent;
    public record QueueIdle : IHybridDbQueueEvent;
    public record QueueFailed(Exception Exception) : IHybridDbQueueEvent;
}