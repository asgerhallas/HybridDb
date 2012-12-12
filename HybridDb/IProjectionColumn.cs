namespace HybridDb
{
    public interface IProjectionColumn : IColumn
    {
        object GetValue(object document);
    }
}