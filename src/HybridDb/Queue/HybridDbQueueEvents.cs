using System;
using System.Threading;

namespace HybridDb.Queue
{
    public interface IHybridDbQueueEvent
    {
        CancellationToken CancellationToken { get; }
    }

    public record SessionBeginning(SessionContext Context, CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record SessionEnded(SessionContext Context, CancellationToken CancellationToken) : IHybridDbQueueEvent;

    public record MessageReceived(MessageContext Context, HybridDbMessage Message, CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record MessageHandling(IDocumentSession Session, MessageContext Context, HybridDbMessage Message, CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record MessageHandled(IDocumentSession Session, MessageContext Context, HybridDbMessage Message, CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record MessageCommitted(IDocumentSession Session, MessageContext Context, HybridDbMessage Message, CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record MessageFailed(MessageContext Context, HybridDbMessage Message, Exception Exception, int NumberOfFailures, CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record PoisonMessage(MessageContext Context, HybridDbMessage Message, Exception Exception, CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record QueueStarting(CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record QueueStopping(CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record QueuePolling(CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record QueueEmpty(CancellationToken CancellationToken) : IHybridDbQueueEvent;
    public record QueueFailed(Exception Exception, CancellationToken CancellationToken) : IHybridDbQueueEvent;
}