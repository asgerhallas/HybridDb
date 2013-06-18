namespace HybridDb.Studio.ViewModels
{
    public interface ISettings
    {
        bool ConnectionIsValid();
        string ConnectionString { get; set; }
    }
}