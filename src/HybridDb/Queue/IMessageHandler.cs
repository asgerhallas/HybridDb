using System.Threading.Tasks;

namespace HybridDb.Queue
{
    public interface IMessageHandler<in T>
    {
        Task Handle(IDocumentSession session, T message);
    }
}
