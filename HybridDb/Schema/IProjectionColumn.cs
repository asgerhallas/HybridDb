namespace HybridDb.Schema
{
    public interface IProjectionColumn
    {
        string Name { get; }
        object GetValue(object document);
    }
}