namespace HybridDb.Studio.Infrastructure
{
    public interface IEventAggregator
    {
        void Subscribe(object subscriber);
        void Publish<TMessage>(TMessage message);
    }
}