using System.Threading.Tasks;

namespace HybridDb.Queue
{
    public interface IMessageHandler<T>
    {
        Task Handle(IDocumentSession session, T message);
    }
}
