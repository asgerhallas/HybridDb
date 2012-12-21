namespace HybridDb.Schema
{
    public interface IProjectionColumn : IColumn
    {
        object GetValue(object document);
    }
}