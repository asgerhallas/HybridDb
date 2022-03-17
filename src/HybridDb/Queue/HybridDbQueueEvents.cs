using System;

namespace HybridDb.Queue
{
    public interface IHybridDbQueueEvent { }

    public record MessageHandling(MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageHandled(MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageFailed(MessageContext Context, HybridDbMessage Message, Exception Exception) : IHybridDbQueueEvent;
    public record PoisonMessage(MessageContext Context, HybridDbMessage Message, Exception Exception) : IHybridDbQueueEvent;
    public record QueueStarting : IHybridDbQueueEvent;
    public record QueueIdle : IHybridDbQueueEvent;
    public record QueueFailed(Exception Exception) : IHybridDbQueueEvent;
}