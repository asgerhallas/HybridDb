using System;
using System.Threading;

namespace HybridDb.Queue
{
    public interface IHybridDbQueueEvent { }

    public record SessionBeginning(SessionContext Context) : IHybridDbQueueEvent;
    public record SessionEnded(SessionContext Context) : IHybridDbQueueEvent;

    public record MessageReceived(MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageHandling(IDocumentSession Session, MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageHandled(IDocumentSession Session, MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageCommitted(IDocumentSession Session, MessageContext Context, HybridDbMessage Message) : IHybridDbQueueEvent;
    public record MessageFailed(MessageContext Context, HybridDbMessage Message, Exception Exception, int NumberOfFailures) : IHybridDbQueueEvent;
    public record PoisonMessage(MessageContext Context, HybridDbMessage Message, Exception Exception) : IHybridDbQueueEvent;
    public record QueueStarting(CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record QueueStopping(CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record QueuePolling(CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record QueueEmpty(CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record QueueFailed(Exception Exception) : IHybridDbQueueEvent;
}