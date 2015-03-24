namespace HybridDb.Studio.Infrastructure.Views
{
    public interface IViewFactory
    {
        IView CreateView(string key);
    }
}